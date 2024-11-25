using Derigo.SP.Utilities;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace TaskReminder
{
    internal class Program
    {
        public static Config config;

        static void Main(string[] args)
        {
            config = GetConfig();
            Helper.GetMailTemplate(config);

            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                Helper.SetThreadCulture(config.Culture);
                SPWebApplication webApp = SPWebApplication.Lookup(new Uri(config.WebAppPath));
                if(webApp == null) throw new Exception("Web application not found!");

                List<UserMailData> allUserMailItems = new List<UserMailData>();
                List<dynamic> allProcessTasks = new List<dynamic>();
                List<dynamic> allProjectTasks = new List<dynamic>();
                SendEmailService sendEmailService = new SendEmailService(config.SMTP);

                string tempDirectoryPath = Helper.CreateTempFolder();
                Helper.Log("- START");

                foreach (SPSite site in webApp.Sites)
                {
                    // WebAppPath contains site
                    if (!site.Url.StartsWith(config.WebAppPath)) { continue; }

                    ProjectFilter projectFilter = config.ProjectFilters.Where(e => String.IsNullOrEmpty(e.SiteUrl) || e.SiteUrl.Equals(site.ServerRelativeUrl)).FirstOrDefault();

                    foreach (SPWeb web in site.RootWeb.Webs)
                    {
                        if (SPHelper.GetWebProperty(web, "Derigo.Pro3.WebType") == "BaseWeb")
                        {
                            Helper.Log("- SITE OPEN: " + web.Url);

                            List<DynamicClass> projects = Helper.GetProjects(web, projectFilter);

                            Helper.Log("-- PROJECTS: " + projects.Count);

                            foreach (SPWeb projectWeb in web.Webs)
                            {
                                int projectId = int.TryParse(projectWeb.Url.Substring(projectWeb.Url.LastIndexOf("/") + 1), out projectId) ? projectId : -1;
                                if (projectId > -1)
                                {
                                    // GET PROJECTITEM
                                    DynamicClass project = projects.Where(e => e.ID == projectId).FirstOrDefault();
                                    if (project != null)
                                    {
                                        project.Add("Link", projectWeb.Url);
                                        project.Add("Web", new Web() { Title = projectWeb.Title, ID = projectWeb.ID, Url = projectWeb.Url });

                                        // PROJECT LEVEL TASKS
                                        foreach (TasksConfig projectTaskConfig in config.ProjectTasks)
                                        {
                                            if (projectTaskConfig.Enabled)
                                            {
                                                List<DynamicClass> taskItems = new TasksService(projectTaskConfig).Get(projectWeb, project);
                                                AddTasksToUserMailItems(taskItems, allUserMailItems, projectTaskConfig);
                                            }
                                        }

                                        foreach (SPWeb processWeb in projectWeb.Webs)
                                        {
                                            // PROCESS LEVEL TASKS
                                            foreach (TasksConfig processTaskConfig in config.ProcessTasks)
                                            {
                                                if (processTaskConfig.Enabled)
                                                {
                                                    List<DynamicClass> taskItems = new TasksService(processTaskConfig).Get(processWeb, project);
                                                    AddTasksToUserMailItems(taskItems, allUserMailItems, processTaskConfig);
                                                }
                                            }
                                            // FORM WORKFLOWS
                                            if (config.MobileForms.WorkFlows.Enabled)
                                            {
                                                List<Form> formWorkflows = new MobileFormsService(config.MobileForms).GetWorkflows(processWeb, project);
                                                AddFormWorkfFlowsToUserMailItems(formWorkflows, allUserMailItems);
                                            }
                                            // FORM NOTIFICATIONS
                                            if (config.MobileForms.Notifications.Enabled)
                                            {
                                                List<Form> formNotifications = new MobileFormsService(config.MobileForms).GetNotifications(processWeb, project);
                                                AddFormNotificationsToUserMailItems(formNotifications, allUserMailItems);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                Helper.Log(String.Format("-- EMAILS TOTAL: {0}", allUserMailItems.Count));

                //RENDER MAILS AND SEND
                using (Renderer renderer = new Renderer())
                {
                    renderer.ErrorOccurred += (sender, e) =>
                    {
                        string logMessage = String.Format("-- RENDER FAILED: " + e.UserMailData.EmailAddress + "-> {0}", e.Exception.Message);
                        Helper.Log(logMessage);
                    };         

                    foreach (UserMailData item in allUserMailItems)
                    {
                        try
                        {
                            item.MailItem = new MailItem();
                            item.MailItem.To = item.EmailAddress;
                            item.MailItem.Subject = config.Email.Header;
                            item.MailItem.Body = renderer.Render(config.Email.BodyTemplate, item);

                            LogUserMailItem(item);

                            Helper.WriteToFile(item.MailItem.Body, item.MailItem.To + ".html", tempDirectoryPath);

                            if (config.Send)
                            {
                                sendEmailService.Send(item.MailItem);
                            }
                        }
                        catch { }                   
                    }
                }

                Helper.Log(String.Format("-- SEND ENABLED: {0}\n- COMPLETE", config.Send));
            });
        }   

        // ADD FORM NOTIFICATIONS
        private static void AddFormNotificationsToUserMailItems(List<Form> formNotifications, List<UserMailData> allUserMailItems)
        {
            foreach (Form formItem in formNotifications)
            {
                if(formItem.ProjectID > 0) {
                    foreach (string email in formItem.Notification.Emails)
                    {
                        UserMailData userMailDataItem = GetUserMailItem(email, allUserMailItems);
                        DynamicClass ProjectUserMailDataItem = GetProjectUserMailDataItem(userMailDataItem, formItem.Project, "FormNotifications");
                        List<Form> tasksList = (List<Form>)ProjectUserMailDataItem["FormNotifications"];
                        tasksList.Add(formItem);
                    }
                }
            }
        }

        // ADD FORM WORKFLOWS
        private static void AddFormWorkfFlowsToUserMailItems(List<Form> formWorkflows, List<UserMailData> allUserMailItems)
        {
            foreach (Form formItem in formWorkflows)
            {
                if (!String.IsNullOrEmpty(formItem.WorkFlow.AssignedTo.Email) && formItem.ProjectID > 0)
                {
                    UserMailData userMailDataItem = GetUserMailItem(formItem.WorkFlow.AssignedTo.Email, allUserMailItems);
                    DynamicClass ProjectUserMailDataItem = GetProjectUserMailDataItem(userMailDataItem, formItem.Project, "FormWorkflows");
                    List<Form> tasksList = (List<Form>)ProjectUserMailDataItem["FormWorkflows"];
                    tasksList.Add(formItem);
                }
            }
        }

        // ADD TASKS
        private static void AddTasksToUserMailItems(List<DynamicClass> taskItems, List<UserMailData> allUserMailItems, TasksConfig tasksConfigItem)
        {
            foreach (var taskItem in taskItems)
            {
                if (taskItem.ProjectID > 0 && taskItem["AssignedTo"] != null)
                {
                   

                    if (taskItem["AssignedTo"] is UserMulti)
                    {
                        UserMulti userMulti = (taskItem["AssignedTo"] as UserMulti);
                        foreach (UserLookup assignedToLookup in userMulti.results)
                        {
                            AddToProjectContainer(assignedToLookup.Email, allUserMailItems, tasksConfigItem.ListName, taskItem);
                        }

                    }
                    else if (taskItem["AssignedTo"] is UserLookup)
                    {
                        UserLookup assignedToLookup = (taskItem["AssignedTo"] as UserLookup);
                        AddToProjectContainer(assignedToLookup.Email, allUserMailItems, tasksConfigItem.ListName, taskItem);
                    }
                }
            }
        }

        private static void AddToProjectContainer(string emailAddress, List<UserMailData> allUserMailItems, string containerName, DynamicClass taskItem)
        {
            if (!String.IsNullOrEmpty(emailAddress))
            {
                DynamicClass project = taskItem["Project"] as DynamicClass;
                UserMailData userMailDataItem = GetUserMailItem(emailAddress, allUserMailItems);
                DynamicClass ProjectUserMailDataItem = GetProjectUserMailDataItem(userMailDataItem, project, containerName);
                List<DynamicClass> tasksList = (List<DynamicClass>)ProjectUserMailDataItem[containerName];
                tasksList.Add(taskItem);
            }
        }

        // Get or create user mail item
        private static UserMailData GetUserMailItem(string email, List<UserMailData> allUserMailItems)
        {
            UserMailData userMailDataItem = (from e in allUserMailItems where e.EmailAddress.Equals(email) select e).FirstOrDefault();
            if (userMailDataItem == null)
            {
                userMailDataItem = new UserMailData() { EmailAddress = email };
                allUserMailItems.Add(userMailDataItem);
                userMailDataItem = (from e in allUserMailItems where e.EmailAddress.Equals(email) select e).FirstOrDefault();
            }

            return userMailDataItem;
        }

        // Get or create project item in user mail data
        private static DynamicClass GetProjectUserMailDataItem(UserMailData userMailDataItem, DynamicClass project, string containerName)
        {
            DynamicClass ProjectUserMailDataItem = Helper.FindProject(userMailDataItem.Projects, project);
            Type type = containerName == "FormNotifications" || containerName == "FormWorkflows" ? typeof(List<Form>) : typeof(List<DynamicClass>);

            if (ProjectUserMailDataItem == null)
            {
                userMailDataItem.Projects.Add(project.Clone());
                ProjectUserMailDataItem = Helper.FindProject(userMailDataItem.Projects, project);
            }

            if (!ProjectUserMailDataItem.Contains(containerName))
            {
                ProjectUserMailDataItem.Add(containerName, Activator.CreateInstance(type));
            }

            return ProjectUserMailDataItem;
        }

        private static void LogUserMailItem(UserMailData item)
        {
            List<DynamicClass> projects = item.Projects;
            int taskCount = 0;
            int projectRisksTasks = 0;
            int workflowCount = 0;
            int notificationCount = 0;

            foreach (DynamicClass project in projects)
            {
                if (project.Contains("Tasks"))
                {
                    taskCount += ((List<DynamicClass>)project["Tasks"]).Count;
                }
                if (project.Contains("ProjectRisksTasks"))
                {
                    projectRisksTasks += ((List<DynamicClass>)project["ProjectRisksTasks"]).Count;
                }
                if (project.Contains("FormWorkflows"))
                {
                    workflowCount += ((List<Form>)project["FormWorkflows"]).Count;
                }
                if (project.Contains("FormNotifications"))
                {
                    notificationCount += ((List<Form>)project["FormNotifications"]).Count;
                }
            }

            var logMessage = String.Format("-- EMAIL: {0} -> Tasks: {1}, ProjectRisksTasks: {2}, Workflows: {3}, Notifications: {4}", item.EmailAddress, taskCount, projectRisksTasks, workflowCount, notificationCount);
            Helper.Log(logMessage);
        }

        private static Config GetConfig()
        {
            string cs = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/Config.json");
            Config config = new JavaScriptSerializer().Deserialize<Config>(cs);
            return config;
        }
    }
}
