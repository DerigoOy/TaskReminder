using Derigo.SP.Utilities;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskReminder
{
    public class TasksService
    {
        private static TasksConfig _tasksConfig;

        public TasksService(TasksConfig tasksConfig)
        {
            _tasksConfig = tasksConfig;
        }

        public List<DynamicClass> Get(SPWeb web, DynamicClass project)
        {
            List<DynamicClass> allTasks = new List<DynamicClass>();

            foreach (FieldConfig fieldConfig in _tasksConfig.FieldConfig)
            {               
                allTasks.AddRange(GetFieldConfigTasks(web, project, fieldConfig));
            }            

            return allTasks.OrderBy(c=> (int)c["BeforeDueDate"]).ToList();
        }

        public List<DynamicClass> GetFieldConfigTasks(SPWeb web, DynamicClass project, FieldConfig fieldConfig)
        {
            List<DynamicClass> tasks = new List<DynamicClass>();

            try
            {
                tasks = GetTasks(web, fieldConfig);
                if (tasks.Count > 0)
                {
                    foreach (DynamicClass task in tasks)
                    {
                        TimeSpan timeDifference = task.GetDateTime(fieldConfig.DueDateField) - DateTime.Now.Date;

                        task.ProjectID = project.GetInteger("ID");
                        task.Add("Link", web.Url);
                        task.Add("DisplayLink", web.Url + "/Lists/" + _tasksConfig.ListName + "/DispForm.aspx?ID=" + task["ID"]);
                        task.Add("Project", project);
                        task.Add("AssignedTo", task[fieldConfig.AssignedToField]);
                        task.Add("DueDate", task.GetDateTime(fieldConfig.DueDateField));
                        task.Add("IsLate", task.GetDateTime(fieldConfig.DueDateField) < DateTime.Now.Date);
                        task.Add("BeforeDueDate", (int)Math.Round(timeDifference.TotalDays));
                        task.Add("Web", new Web() { Title = web.Title, ID = web.ID, Url = web.Url });
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.Log(ex.ToString());
            }

            return tasks;
        }

        public List<DynamicClass> GetTasks(SPWeb web, FieldConfig fieldConfig)
        {
            string completeValue = SPUtility.GetLocalizedString("$Resources:Tasks_Completed;", "core", web.Language);
            SPList tasksList = SPHelper.GetListByName(web, "Lists/" + _tasksConfig.ListName);
            List<DynamicClass> tasks = new List<DynamicClass>();

            if (tasksList != null)
            {
                string dueDateFieldTitle = tasksList.Fields.GetFieldByInternalName(fieldConfig.DueDateField).Title;

                if (_tasksConfig.Query == null)
                {
                    string statusFieldType = tasksList.Fields.GetFieldByInternalName(fieldConfig.StatusField).TypeAsString;
                    if (statusFieldType == "Boolean")
                    {
                        _tasksConfig.Query =
                        String.Format("<Where>" +
                            "<And>" +
                                "<IsNotNull><FieldRef Name='{0}'/></IsNotNull>" +
                                "<And>" +
                                    "<IsNotNull><FieldRef Name='{1}'/></IsNotNull>" +
                                    "<Neq><FieldRef Name='{2}'/><Value Type='Boolean'>1</Value></Neq>" +
                                "</And>" +
                            "</And>" +
                        "</Where>", fieldConfig.DueDateField, fieldConfig.AssignedToField, fieldConfig.StatusField);
                    }
                    else
                    {
                        _tasksConfig.Query =
                       String.Format("<Where>" +
                           "<And>" +
                               "<IsNotNull><FieldRef Name='{0}'/></IsNotNull>" +
                               "<And>" +
                                   "<IsNotNull><FieldRef Name='{1}'/></IsNotNull>" +
                                   "<Neq><FieldRef Name='{2}'/><Value Type='{3}'>{4}</Value></Neq>" +
                               "</And>" +
                           "</And>" +
                       "</Where>", fieldConfig.DueDateField, fieldConfig.AssignedToField, fieldConfig.StatusField, statusFieldType, completeValue);
                    }
                }

                SPQuery query = new SPQuery();
                query.Query = _tasksConfig.Query;
                query.RowLimit = int.MaxValue;
                query.QueryThrottleMode = SPQueryThrottleOption.Override;
                query.ViewAttributes = "Scope=\"RecursiveAll\"";
                SPListItemCollection taskItems = tasksList.GetItems(query);

                foreach (SPListItem taskItem in FilterTasks(taskItems, fieldConfig))
                {
                    DynamicClass task = ConvertListItem.ToDynamic(taskItem);
                    task.Add("DueDateFieldTitle", dueDateFieldTitle);
                    tasks.Add(task);
                }
            }

            return tasks;
        }

        public List<SPListItem> FilterTasks(SPListItemCollection taskItems, FieldConfig fieldConfig)
        {
            List<SPListItem> filtered = new List<SPListItem>();

            foreach (SPListItem task in taskItems)
            {
                DateTime? dueDate = task[fieldConfig.DueDateField] as DateTime?;
                if (dueDate.HasValue)
                {
                    foreach (Filter filter in _tasksConfig.Filters)
                    {
                        if (filter.WeekDay.HasValue)
                        {
                            if ((int)DateTime.Now.DayOfWeek == filter.WeekDay.Value)
                            {
                                int until = filter.UntilOffsetDays.HasValue ? filter.UntilOffsetDays.Value : -365;
                                DateTime firstDate = DateTime.Now.AddDays(until);
                                DateTime lastDate = DateTime.Now.AddDays(filter.OffsetDays);

                                if (dueDate.Value.Date >= firstDate && dueDate.Value.Date <= lastDate)
                                {
                                    filtered.Add(task);
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
                                    if (dueDate.Value.Date > DateTime.Now.AddDays(offset).Date)
                                    {
                                        break;
                                    }
                                    else if (dueDate.Value.Date == DateTime.Now.AddDays(offset).Date)
                                    {
                                        filtered.Add(task);
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                for (int offset = filter.OffsetDays; offset < until; offset += repeat)
                                {
                                    if (dueDate.Value.Date < DateTime.Now.AddDays(offset).Date)
                                    {
                                        break;
                                    }
                                    else if (dueDate.Value.Date == DateTime.Now.AddDays(offset).Date)
                                    {
                                        filtered.Add(task);
                                        break;
                                    }
                                }
                            }

                        }
                        else
                        {
                            if (dueDate.Value.Date == DateTime.Now.AddDays(filter.OffsetDays).Date)
                            {
                                filtered.Add(task);
                                break;
                            }
                        }
                    }
                }
            }

            return filtered;
        }
    }
}
