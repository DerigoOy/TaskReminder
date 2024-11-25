using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Net;
using System.Runtime.Serialization;

namespace TaskReminder
{
    public class Config
    {
        public int Culture { get; set; }
        public bool LogCounts { get; set; }
        public string WebAppPath { get; set; }
        public SMTPConfig SMTP { get; set; }
        public EmailConfig Email { get; set; }
        public List<ProjectFilter> ProjectFilters { get; set; }
        public List<TasksConfig> ProjectTasks { get; set; }
        public List<TasksConfig> ProcessTasks { get; set; }
        public MobileFormsConfig MobileForms { get; set; } = new MobileFormsConfig();
        public bool Send { get; set; }

        public Config()
        {
            ProjectFilters = new List<ProjectFilter>();
            ProjectTasks = new List<TasksConfig>();
            ProcessTasks = new List<TasksConfig>();
        }
    }

    public class EmailConfig
    {
        public string Header { get; set; }
        public string BodyTemplateFile { get; set; }
        public string BodyTemplate { get; set; }
    }

    public class ProjectFilter
    {
        public string SiteUrl { get; set; }
        public string Query { get; set; }
        public List<FieldFilter> FieldFilters { get; set; }

        public ProjectFilter()
        {
            FieldFilters = new List<FieldFilter>();
        }
    }

    public class TasksConfig
    {
        public bool Enabled { get; set; }
        public string ListName { get; set; }
        public string Query { get; set; }
        public List<FieldConfig> FieldConfig { get; set; }
        public List<Filter> Filters { get; set; }

        public TasksConfig()
        {
            FieldConfig = new List<FieldConfig>() { new FieldConfig() };
            Filters = new List<Filter>();
        }
    }

    public class FieldConfig
    {
        public string DueDateField { get; set; } = "DueDate";
        public string StatusField { get; set; } = "Status";
        public string AssignedToField { get; set; } = "AssignedTo";
    }

    public class MobileFormsConfig
    {
        public FormWorkFlowsConfig WorkFlows { get; set; } = new FormWorkFlowsConfig();
        public FormNotificationsConfig Notifications { get; set; } = new FormNotificationsConfig();
    }

    public class FormNotificationsConfig
    {
        public bool Enabled { get; set; }
        public List<Filter> Filters { get; set; }

        public FormNotificationsConfig()
        {
            Filters = new List<Filter>();
        }
    }

    public class FormWorkFlowsConfig
    {
        public bool Enabled { get; set; }
        public List<Filter> Filters { get; set; }

        public FormWorkFlowsConfig()
        {
            Filters = new List<Filter>();
        }
    }

    public class Filter
    {
        public string FieldName { get; set; }
        public int? WeekDay { get; set; }
        public int OffsetDays { get; set; } = 0;
        public int? Repeat { get; set; }
        public int? UntilOffsetDays { get; set; }
    }

    public class FieldFilter
    {
        public string FieldName { get; set; }
        public string Value { get; set; }
    }

    public class SMTPConfig
    {
        public string Host { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string AllEmailsTo { get; set; }
        public string OnlySendTo { get; set; }
        public int Timeout { get; set; } = 10000;
        public int Port { get; set; } = 25;
        public bool SSL { get; set; } = false;
        public NetworkCredential Credentials { get; set; } = CredentialCache.DefaultNetworkCredentials;
    }


    [Serializable]
    public class UserMailData
    {
        public string EmailAddress { get; set; }
        public MailItem MailItem { get; set; }
        public List<DynamicClass> Projects { get; set; }
        public UserMailData()
        {
            Projects = new List<DynamicClass>();
        }

        public string GetFormattedDate(DateTime? date)
        {
            if (date.HasValue)
            {
                return date.Value.ToShortDateString();
            }
            return "";
        }

        public string GetFormattedDateTime(DateTime? date)
        {
            if (date.HasValue)
            {
                return date.Value.ToShortDateString() + " " + date.Value.ToShortTimeString();
            }
            return "";
        }
    }

    [Serializable]
    public class MailItem
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Body { get; set; }
        public string Subject { get; set; }
    }

    [Serializable]
    public class Form
    {
        public string Title { get; set; }
        public int Id { get; set; }
        public DateTime? Created { get; set; }
        public UserLookup CreatedBy { get; set; }
        public DateTime? Modified { get; set; }
        public UserLookup ModifiedBy { get; set; }
        public Dictionary<string, object> FormData { get; set; }

        public ArrayList Sets { get; set; }
        public string EditLink { get; set; }
        public int ProjectID { get; set; }
        public DynamicClass Project { get; set; }
        public string ProjectLink { get; set; }
        public FormWorkflow WorkFlow { get; set; }
        public bool HasWorkFlow { get; set; }
        public FormNotification Notification { get; set; }
        public bool HasNotification { get; set; }

        public Form()
        {
            FormData = new Dictionary<string, object>();
            Sets = new ArrayList();
            WorkFlow = new FormWorkflow();
            Notification = new FormNotification();
        }
    }

    [Serializable]
    public class FormNotification
    {
        public List<string> Emails { get; set; }
        public List<Dictionary<string, object>> Sets { get; set; }
        public FormNotification()
        {
            Emails = new List<string>();
            Sets = new List<Dictionary<string, object>>();
        }
    }

    [Serializable]
    public class FormWorkflow
    {
        public UserLookup AssignedTo { get; set; }
        public bool Ready { get; set; }
        public DateTime? Time { get; set; }
        public string Step { get; set; }
        public string Status { get; set; }
        public List<Dictionary<string, object>> Sets { get; set; }

        public FormWorkflow()
        {
            Sets = new List<Dictionary<string, object>>();
        }
    }

    [Serializable]
    public class Step
    {
    }

    [Serializable]
    public class Lookup
    {
        public string Title { get; set; }
        public int ID { get; set; }
    }

    [Serializable]
    public class UserLookup
    {
        public string Title { get; set; }
        public string Email { get; set; }
        public int ID { get; set; }
    }

    [Serializable]
    public class LookupMulti
    {
        public LookupMulti()
        {
            this.results = new List<Lookup>();
        }

        public List<Lookup> results { get; set; }
    }

    [Serializable]
    public class UserMulti
    {
        public UserMulti()
        {
            this.results = new List<UserLookup>();
        }

        public string GetUserNames()
        {
            string names = "";
            foreach (UserLookup user in results)
            {
                // if not last add comma
                if (results.IndexOf(user) != results.Count - 1)
                {
                    names += user.Title + ", ";
                }
                else
                {
                    names += user.Title;
                }
            }
            return names;
        }

        public List<UserLookup> results { get; set; }
    }

    [Serializable]
    public class Web
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public Guid ID { get; set; }
    }
}