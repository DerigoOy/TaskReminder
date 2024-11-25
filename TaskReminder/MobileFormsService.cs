using Derigo.SP.Utilities;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.Workflow;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace TaskReminder
{
    public class MobileFormsService
    {
        private static MobileFormsConfig _mobileFormsConfig;

        public MobileFormsService(MobileFormsConfig mobileFormsConfig)
        {
            _mobileFormsConfig = mobileFormsConfig;
        }

        public List<Form> GetWorkflows(SPWeb web, DynamicClass project)
        {
            List<Form> expandoList = new List<Form>();
            List<Form> workflowForms = GetWorkflowForms(web);
            workflowForms = FilterWorkflowForms(workflowForms);

            if (workflowForms.Count > 0)
            {
                SPList formsList = SPHelper.GetListByName(web, "Lists/MobileForms");
                if (formsList != null)
                {
                    foreach (Form form in workflowForms)
                    {
                        SPListItem formListItem = GetFormListItem(web, formsList, form);
                        bool formDataIsSet = SetFormData(web, form, formListItem);
                        if (formDataIsSet)
                        {
                            form.WorkFlow.Sets = GetWorkflowSets(form.Sets);
                            form.HasWorkFlow = form.WorkFlow.Sets.Count > 0 ? true : false;
                            // Ready should be true, but check form actual data
                            form.WorkFlow.Ready = form.HasWorkFlow && FormWorkflowIsComplete(form.Sets) ? true : false;
                            if (formDataIsSet && form.HasWorkFlow && !form.WorkFlow.Ready)
                            {
                                form.ProjectID = project.GetInteger("ID").Value;
                                form.Project = project;
                            }
                        }
                    }
                }
            }

            return workflowForms.Where(n => n.Project != null).ToList();
        }

        public List<Form> GetNotifications(SPWeb web, DynamicClass project)
        {
            List<Form> formNotifications = new List<Form>();
            SPList formsList = SPHelper.GetListByName(web, "Lists/MobileForms");
            if (formsList != null)
            {
                foreach (Filter filter in _mobileFormsConfig.Notifications.Filters)
                {
                    if ((int)DateTime.Now.DayOfWeek == filter.WeekDay.Value)
                    {
                        int until = filter.UntilOffsetDays.HasValue ? filter.UntilOffsetDays.Value : -365;
                        DateTime firstDate = DateTime.Now.AddDays(until);
                        DateTime lastDate = DateTime.Now.AddDays(filter.OffsetDays);
                        SPQuery sPQuery = new SPQuery();
                        sPQuery.Query = String.Format(@"<Where>
                            <And>
                                <Geq><FieldRef Name='{0}' /><Value Type='DateTime'>" + SPUtility.CreateISO8601DateTimeFromSystemDateTime(firstDate) + "</Value></Geq>" +
                                "<Leq><FieldRef Name='{0}' /><Value Type='DateTime'>" + SPUtility.CreateISO8601DateTimeFromSystemDateTime(lastDate) + "</Value></Leq>" +
                           "</And>" +
                        "</Where>", filter.FieldName);

                        SPListItemCollection formItems = formsList.GetItems(sPQuery);
                        foreach (SPListItem formListItem in formItems)
                        {
                            Form form = new Form();
                            bool formDataIsSet = SetFormData(web, form, formListItem);
                            if (formDataIsSet)
                            {
                                form.Notification.Sets = GetNotificationSet(form.Sets);
                                form.HasNotification = form.Notification.Sets.Count > 0 ? true : false;
                                if (form.HasNotification)
                                {
                                    form.Notification.Emails = GetNotificationEmailAddresses(form);
                                    form.ProjectID = project.GetInteger("ID").Value;
                                    form.Project = project;
                                    formNotifications.Add(form);
                                }
                            }
                        }
                    }
                }
            }

            return formNotifications.Where(n => n.Project != null).ToList();
        }

        public SPListItem GetFormListItem(SPWeb web, SPList formsList, Form form)
        {
            SPListItem formItem = null;
            try
            {
                formItem = formsList.GetItemById(form.Id);
            }
            catch { }
            return formItem;
        }

        public bool SetFormData(SPWeb web, Form form, SPListItem formListItem)
        {
            bool success = false;
            try
            {
                var editLink = formListItem["EditLink"] + "";
                string serverUrl = new Uri(web.Site.RootWeb.Url).GetLeftPart(UriPartial.Authority);

                form.Created = formListItem["Created"] as DateTime?;
                form.Modified = formListItem["Modified"] as DateTime?;
                form.CreatedBy = Helper.GetUser(web, formListItem["Author"] + "");
                form.ModifiedBy = Helper.GetUser(web, formListItem["Editor"] + "");
                form.Title = formListItem.Title;
                form.EditLink = editLink.Substring(0, editLink.IndexOf("/")) + serverUrl + editLink.Substring(editLink.IndexOf("/"));
                form.FormData = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(formListItem["data"] + "");
                form.Sets = form.FormData.ContainsKey("sets") == true ? (ArrayList)form.FormData["sets"] : null;
                success = true;
            }
            catch (Exception)
            {
                success = false;
            }

            return success;
        }

        public List<Form> FilterWorkflowForms(List<Form> workflows)
        {
            List<Form> filtered = new List<Form>();
            foreach (Form wf in workflows)
            {
                if (wf.WorkFlow.Ready == false && wf.WorkFlow.Time.HasValue)
                {
                    foreach (Filter filter in _mobileFormsConfig.WorkFlows.Filters)
                    {
                        if (filter.WeekDay.HasValue)
                        {
                            if ((int)DateTime.Now.DayOfWeek == filter.WeekDay.Value)
                            {
                                int until = filter.UntilOffsetDays.HasValue ? filter.UntilOffsetDays.Value : -365;
                                DateTime firstDate = DateTime.Now.AddDays(until);
                                DateTime lastDate = DateTime.Now.AddDays(filter.OffsetDays);

                                if (wf.WorkFlow.Time.Value.Date >= firstDate && wf.WorkFlow.Time.Value.Date != DateTime.Now.Date)
                                {
                                    filtered.Add(wf);
                                    break;
                                }
                            }
                        }
                        else if (filter.Repeat.HasValue)
                        {
                            bool offsetShrinking = filter.Repeat.Value < 0;
                            int repeat = filter.Repeat.Value;
                            int until = filter.UntilOffsetDays.HasValue ? filter.UntilOffsetDays.Value : (offsetShrinking ? -365 : 365);

                            if (offsetShrinking)
                            {
                                for (int offset = filter.OffsetDays; offset > until; offset += repeat)
                                {
                                    if (wf.WorkFlow.Time.Value.Date > DateTime.Now.AddDays(offset).Date)
                                    {
                                        break;
                                    }
                                    else if (wf.WorkFlow.Time.Value.Date == DateTime.Now.AddDays(offset).Date)
                                    {
                                        filtered.Add(wf);
                                        break;
                                    }
                                }
                            }
                            else
                            {

                                for (int offset = filter.OffsetDays; offset < until; offset += repeat)
                                {
                                    if (wf.WorkFlow.Time.Value.Date < DateTime.Now.AddDays(offset).Date)
                                    {
                                        break;
                                    }
                                    else if (wf.WorkFlow.Time.Value.Date == DateTime.Now.AddDays(offset).Date)
                                    {
                                        filtered.Add(wf);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (wf.WorkFlow.Time.Value.Date == DateTime.Now.AddDays(filter.OffsetDays).Date)
                            {
                                filtered.Add(wf);
                                break;
                            }
                        }
                    }
                }
            }

            return filtered;
        }

        public UserLookup GetUserById(SPWeb web, string userIdStr)
        {
            UserLookup userLookup = new UserLookup();
            int userId = int.TryParse(userIdStr, out userId) ? userId : -1;
            if (userId > 0)
            {
                SPUser spuser = (from SPUser u in web.SiteUsers
                                 where u.ID.Equals(userId)
                                 select u).FirstOrDefault();
                if (spuser != null)
                {
                    userLookup.Title = spuser.Name;
                    userLookup.ID = spuser.ID;
                    userLookup.Email = spuser.Email;
                }
            }
            return userLookup;
        }

        public List<Form> GetWorkflowForms(SPWeb web)
        {
            List<KeyValuePair<string, string>> keyValuePairs = new List<KeyValuePair<string, string>>();
            foreach (string key in web.AllProperties.Keys)
            {
                if (!key.ToString().StartsWith("workflow."))
                {
                    continue;
                }

                keyValuePairs.Add(new KeyValuePair<string, string>(key, web.GetProperty(key) + ""));
            }

            List<Form> workflows = new List<Form>();

            foreach (var kvp in keyValuePairs)
            {
                string[] parts = kvp.Key.Split('.');
                if (parts.Length == 3 && parts[0] == "workflow")
                {
                    string idPart = parts[1];
                    string keyPart = parts[2];
                    int id = int.TryParse(idPart, out id) ? id : -1;

                    if (id > -1)
                    {
                        Form existingWorkflow = workflows.FirstOrDefault(w => w.Id == id);
                        if (existingWorkflow == null)
                        {
                            existingWorkflow = new Form { Id = id };
                            workflows.Add(existingWorkflow);
                            existingWorkflow = workflows.FirstOrDefault(w => w.Id == id);
                        }

                        if (keyPart == "to")
                        {
                            existingWorkflow.WorkFlow.AssignedTo = GetUserById(web, kvp.Value);
                        }
                        else if (keyPart == "ready")
                        {
                            existingWorkflow.WorkFlow.Ready = bool.TryParse(kvp.Value, out bool ready) ? ready : false;
                        }
                        else if (keyPart == "step")
                        {
                            existingWorkflow.WorkFlow.Step = kvp.Value;
                        }
                        else if (keyPart == "status")
                        {
                            existingWorkflow.WorkFlow.Status = kvp.Value;
                        }
                        else if (keyPart == "time")
                        {
                            DateTime time;
                            bool success = DateTime.TryParse(kvp.Value, out time);
                            if (success) existingWorkflow.WorkFlow.Time = time;
                        }
                    }
                }
            }

            return workflows;
        }

        public bool FormWorkflowIsComplete(ArrayList sets)
        {
            bool workflowIsComplete = false;
            Dictionary<string, object> lastWorkflowSet = null;

            foreach (Dictionary<string, object> set in sets)
            {
                bool isWorkflow = set.ContainsKey("isworkflow") ? (bool.TryParse(set["isworkflow"] + "", out isWorkflow) ? isWorkflow : false) : false;
                if (isWorkflow)
                {
                    lastWorkflowSet = set;
                }
            }

            if (lastWorkflowSet != null)
            {
                ArrayList fields = (ArrayList)lastWorkflowSet["fields"];
                Dictionary<string, object> cpField = null;

                foreach (Dictionary<string, object> field in fields)
                {
                    string type = field.ContainsKey("type") ? field["type"] + "" : "";
                    if (type == "cb")
                    {
                        cpField = field;
                        break;
                    }
                }

                if (cpField != null)
                {
                    ArrayList options = (ArrayList)cpField["options"];
                    int index = 0;
                    foreach (Dictionary<string, object> option in options)
                    {
                        bool isChecked = option.ContainsKey("value") ? (bool.TryParse(option["value"] + "", out isChecked) ? isChecked : false) : false;
                        if (index == 0 && isChecked) // Last set is Approved
                        {
                            workflowIsComplete = true;
                            break;
                        }

                        if (index == 1 && isChecked) //  Last set is Rejected
                        {
                            workflowIsComplete = true;
                            break;
                        }
                        index++;
                    }
                }
            }

            return workflowIsComplete;
        }

        public List<Dictionary<string, object>> GetWorkflowSets(ArrayList sets)
        {
            List<Dictionary<string, object>> workflowSets = new List<Dictionary<string, object>>();

            foreach (Dictionary<string, object> set in sets)
            {
                if (set.ContainsKey("isworkflow") && set["isworkflow"] != null && ((bool)set["isworkflow"]) == true)
                {
                    workflowSets.Add(set);
                }
            }

            return workflowSets;
        }

        public List<Dictionary<string, object>> GetNotificationSet(ArrayList sets)
        {
            List<Dictionary<string, object>> notificationSets = new List<Dictionary<string, object>>();

            foreach (Dictionary<string, object> set in sets)
            {
                if (set.ContainsKey("isnotification") && set["isnotification"] != null && ((bool)set["isnotification"]) == true)
                {
                    notificationSets.Add(set);
                }
            }

            return notificationSets;
        }

        public List<string> GetNotificationEmailAddresses(Form form)
        {
            List<string> emails = new List<string>();
            foreach (Dictionary<string, object> set in form.Notification.Sets)
            {
                ArrayList fields = (ArrayList)set["fields"];
                foreach (Dictionary<string, object> field in fields)
                {
                    if (field.ContainsKey("type") && field["type"] + "" == "note")
                    {
                        string emailValue = field.ContainsKey("value") ? field["value"] + "" : "";
                        if (!string.IsNullOrEmpty(emailValue))
                        {
                            List<string> emailAddresses = emailValue.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                            emails.AddRange(emailAddresses);
                        }
                    }
                }
            }

            return emails;
        }
    }
}
