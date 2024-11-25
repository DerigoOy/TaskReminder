using Derigo.SP.Utilities;
using Microsoft.SharePoint;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TaskReminder
{
    internal class Helper
    {
        private static bool _dateLogged = false;

        public static void SetThreadCulture(int culture)
        {
            CultureInfo originalUICulture = Thread.CurrentThread.CurrentUICulture;
            CultureInfo ci = new CultureInfo(culture);
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
        }

        public static UserLookup GetUser(SPWeb web, string value)
        {
            UserLookup uLookup = new UserLookup();
            if (!String.IsNullOrEmpty(value))
            {
                SPFieldLookupValue uFieldLookup = new SPFieldLookupValue(value);
                SPUser user = web.SiteUsers.GetByID(uFieldLookup.LookupId);

                if (user != null)
                {
                    uLookup.Title = user.Name;
                    uLookup.ID = user.ID;
                    uLookup.Email = user.Email;
                }
            }
            return uLookup;
        }

        public static int GetProjectID(string url)
        {
            string[] parts = url.Split('/');
            int index = Array.IndexOf(parts, "projects");
            int id = int.TryParse(parts[index + 1], out id) ? id : 0;
            return id;
        }

        public static string CamlOr(List<string> parts)
        {
            if (parts.Count == 0) return "";
            while (parts.Count >= 2)
            {
                List<string> parts2 = new List<string>();

                for (int i = 0; i < parts.Count; i += 2)
                {
                    if (parts.Count == i + 1)
                        parts2.Add(parts[i]);
                    else
                        parts2.Add("<Or>" + parts[i] + parts[i + 1] + "</Or>");
                }

                parts = parts2;
            }

            return parts[0];
        }

        public static DynamicClass GetProject(SPWeb web, int projectId)
        {
            DynamicClass project = new DynamicClass();
            SPList projectsList = SPHelper.GetListByName(web, "Lists/Projects");
            if (projectsList != null)
            {
                try
                {
                    project = ConvertListItem.ToDynamic(projectsList.GetItemById(projectId));
                }
                catch (Exception) {}
            }

            return project;
        }

        public static List<DynamicClass> GetProjects(SPWeb web, ProjectFilter projectFilter)
        {
            List<DynamicClass> projects = new List<DynamicClass>();
            SPQuery query = new SPQuery();
            query.RowLimit = uint.MaxValue;
            query.QueryThrottleMode = SPQueryThrottleOption.Override;

            if (projectFilter != null)
            {
                query.Query = projectFilter.Query;
            }

            SPList projectsList = SPHelper.GetListByName(web, "Lists/Projects");
            if (projectsList != null)
            {
                SPListItemCollection projectItems = projectsList.GetItems(query);
                foreach (SPListItem project in projectItems)
                {
                    try
                    {
                        DynamicClass dynamicProject = ConvertListItem.ToDynamic(project);
                        projects.Add(dynamicProject);
                    }
                    catch (Exception) { }
                }
            }

            return projects;
        }

        public static void AddDTRowsToList(DataTable dt, List<DynamicClass> list)
        {
            foreach (DataRow row in dt.Rows)
            {
                DynamicClass expandoDict = new DynamicClass();
                foreach (DataColumn col in dt.Columns)
                {
                    expandoDict.Add(col.ToString(), row[col.ColumnName].ToString());
                }

                list.Add(expandoDict);
            }
        }

        public static string GetStringValue(DynamicClass expando, string field)
        {
            if (expando == null) return "";
            var expandoDict = expando as IDictionary<String, object>;
            return expandoDict.ContainsKey(field) ? expandoDict[field] + "" : "";
        }

        public static string GetDateValue(DynamicClass expando, string field)
        {
            DateTime date;
            string value = expando.ContainsKey(field) ? expando[field] + "" : null;           
            bool success = DateTime.TryParse(value, out date);
            return success ? date.ToShortDateString() : "";
        }

        public static string GetProcessUrl(string projectsWebUrl, string url)
        {
            url = url.ToLower();
            string[] p = projectsWebUrl.Split("/".ToCharArray());
            p = p.Reverse().ToArray();

            string curl = url.Substring(url.IndexOf(p[0]) + p[0].Length);
            curl = curl.Substring(0, curl.IndexOf("l"));

            return projectsWebUrl + curl;
        }

        public static DynamicClass FindProject(List<DynamicClass> projects, dynamic project)
        {
            string projectLink = GetStringValue(project, "Link");
            foreach (dynamic p in projects)
            {
                if (p.Link == projectLink)
                {
                    return p;
                }
            }
            return null;
        }

        public static IDictionary<string, object> ToDictionary(object obj)
        {
            var result = new Dictionary<string, object>();
            var dictionary = obj as IDictionary<string, object>;
            foreach (var item in dictionary)
                result.Add(item.Key, item.Value);
            return result;
        }

        public static string CreateTempFolder()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string tempDirectory = Path.Combine(baseDirectory, "temp");
            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }
            else
            {
                Directory.GetFiles(tempDirectory).ToList().ForEach(File.Delete);
            }

            return tempDirectory;
        }

        public static void WriteToFile(string content, string filename, string directoryPath)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(Path.Combine(directoryPath, filename), false))
                {
                    writer.WriteLine(content);
                }
            }
            catch (Exception)
            {
            }
        }

        public static void Log(string message)
        {
            try
            {
                string logFilePath = AppDomain.CurrentDomain.BaseDirectory + "Log.txt";
                if (File.Exists(logFilePath) && File.GetCreationTime(logFilePath) < DateTime.Now.AddMonths(-6))
                {
                    File.Delete(logFilePath);
                }

                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    if (!_dateLogged)
                    {
                        writer.WriteLine("--------------------------------------------");
                        writer.WriteLine("-------- DATE : " + DateTime.Now.ToString() + " ---------");
                        writer.WriteLine("--------------------------------------------");
                        _dateLogged = true;
                    }

                    Console.WriteLine(message);
                    writer.WriteLine(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void GetMailTemplate(Config config)
        {
            string templateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.Email.BodyTemplateFile);
            if (File.Exists(templateFilePath))
            {
                string templateContent = File.ReadAllText(templateFilePath);
                if (!string.IsNullOrEmpty(templateContent))
                {
                    config.Email.BodyTemplate = templateContent;
                }
                else
                {
                    Helper.Log("Template file is empty: " + templateFilePath);
                }
            }
            else
            {
                Helper.Log("Template file not found: " + templateFilePath);
            }
        }

        public static DynamicClass CloneObject(DynamicClass obj)
        {
            return JsonConvert.DeserializeObject<DynamicClass>(JsonConvert.SerializeObject(obj));
        }
    }
}
