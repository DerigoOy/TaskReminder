using Microsoft.SharePoint;
using Microsoft.Web.Hosting.Administration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskReminder
{
    internal class ConvertListItem
    {
        public static DynamicClass ToDynamic(SPListItem spItem)
        {
            DynamicClass expando = new DynamicClass();
            expando.ID = spItem.ID;

            if (spItem["FileRef"] != null)
            {
                AddProperty(expando, "FileRef", GetStringValue(spItem, "FileRef"));
            }

            foreach (SPField field in spItem.ParentList.Fields)
            {
                if (field.Hidden != true)
                {
                    string internalName = field.InternalName;

                    if (internalName == "FileRef" || internalName == "FileDirRef")
                    {
                        AddProperty(expando, field.EntityPropertyName, GetStringValue(spItem, internalName));
                    }
                    else if (field.TypeAsString == "DateTime")
                    {
                        AddProperty(expando, field.EntityPropertyName, GetDateStringValue(spItem, internalName));
                    }
                    else if (field.TypeAsString == "User")
                    {
                        AddProperty(expando, field.EntityPropertyName, GetUser(spItem, internalName));
                    }
                    else if (field.TypeAsString == "Lookup")
                    {
                        AddProperty(expando, field.EntityPropertyName, GetLookup(spItem, internalName));
                    }
                    else if (field.TypeAsString == "UserMulti") {
                        AddProperty(expando, field.EntityPropertyName, GetUserMulti(spItem, internalName));
                    }
                    else if (field.TypeAsString == "LookupMulti")
                    {
                        AddProperty(expando, field.EntityPropertyName, GetLookupMulti(spItem, internalName));
                    }
                    else if (field.TypeAsString == "Interger" || field.TypeAsString == "Counter")
                    {
                        AddProperty(expando, field.EntityPropertyName, GetIntegerValue(spItem, internalName));
                        if (field.EntityPropertyName == "ID")
                        {
                            AddProperty(expando, field.EntityPropertyName, GetIntegerValue(spItem, internalName));
                        }
                    }
                    else if (field.TypeAsString == "Number")
                    {
                        AddProperty(expando, field.EntityPropertyName, GetNumberValue(spItem, internalName));
                    }
                    else
                    {
                        AddProperty(expando, field.EntityPropertyName, GetStringValue(spItem, internalName));
                    }
                }
            }

            return expando;
        }

        public static void AddProperty(DynamicClass expando, string propertyName, object propertyValue)
        {
            var expandoDict = expando as IDictionary<String, object>;
            if (expandoDict.ContainsKey(propertyName))
                expandoDict[propertyName] = propertyValue;
            else
                expandoDict.Add(propertyName, propertyValue);
        }

        private static string GetStringValue(SPListItem item, string internalName)
        {
            return item[internalName] != null ? item[internalName] + "" : "";
        }

        private static int? GetIntegerValue(SPListItem item, string internalName)
        {
            return item[internalName] != null ? (int?)item[internalName] : null;
        }

        private static decimal? GetNumberValue(SPListItem item, string internalName)
        {
            if (item[internalName] != null && item[internalName].GetType() == typeof(double))
            {
                double? val = item[internalName] != null ? (double?)item[internalName] : null;
                return (decimal?)val;
            }
            else
            {
                return item[internalName] != null ? (decimal?)item[internalName] : null;
            }
        }

        private static string GetDateStringValue(SPListItem item, string internalName)
        {
            return item[internalName] != null ? DateTime.Parse(item[internalName] + "").ToString("o") : "";
        }

        private static Lookup GetLookup(SPListItem item, string internalName)
        {
            SPFieldLookupValue lookupValue = null;
            if (item[internalName] != null)
            {
                lookupValue = new SPFieldLookupValue(item[internalName] + "");
                return new Lookup()
                {
                    Title = lookupValue.LookupValue,
                    ID = lookupValue.LookupId
                };
            }

            return null;
        }

        private static LookupMulti GetLookupMulti(SPListItem item, string internalName)
        {
            LookupMulti lookupValue = new LookupMulti();
            SPFieldLookupValueCollection lookupValues = null;
            if (item[internalName] != null)
            {
                lookupValues = new SPFieldLookupValueCollection(item[internalName] + "");
                foreach (SPFieldLookupValue lv in lookupValues)
                {
                    lookupValue.results.Add(new Lookup()
                    {
                        Title = lv.LookupValue,
                        ID = lv.LookupId
                    });
                }
            }

            return lookupValue;
        }

        private static UserLookup GetUser(SPListItem item, string internalName)
        {
            UserLookup uLookup = new UserLookup();
            if (item[internalName] != null)
            {
                SPFieldUserValue uFieldUser = new SPFieldUserValue(item.Web, item[internalName] + "");

                if (uFieldUser.User != null)
                {
                    uLookup.Title = uFieldUser.User.Name;
                    uLookup.ID = uFieldUser.User.ID;
                    uLookup.Email = uFieldUser.User.Email;
                }
            }
            return uLookup;
        }

        private static UserMulti GetUserMulti(SPListItem item, string internalName)
        {
            UserMulti lookupValue = new UserMulti();
            if (item[internalName] != null)
            {
                SPFieldUserValueCollection userValues = new SPFieldUserValueCollection(item.Web, item[internalName] + "");

                foreach (SPFieldUserValue lv in userValues)
                {
                    if (lv.User != null)
                    {
                        lookupValue.results.Add(new UserLookup()
                        {
                            Title = lv.User.Name,
                            ID = lv.User.ID,
                            Email = lv.User.Email
                        });
                    }
                }
            }

            return lookupValue;
        }
    }
}
