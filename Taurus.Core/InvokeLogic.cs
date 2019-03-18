﻿using CYQ.Data;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Taurus.Core
{
    /// <summary>
    /// 反射Controller类
    /// </summary>
    static class InvokeLogic
    {
        internal const string Default = "Default";
        internal const string Controller = "Controller";
        internal const string DefaultController = "DefaultController";
        internal const string TaurusController = "Taurus.Core.Controller";
        internal const string CheckToken = "CheckToken";
        internal const string BeforeInvoke = "BeforeInvoke";
        internal const string EndInvoke = "EndInvoke";

        internal const string TokenAttribute = "TokenAttribute";
        internal const string HttpGetAttribute = "HttpGetAttribute";
        internal const string HttpPostAttribute = "HttpPostAttribute";
        internal const string HttpHeadAttribute = "HttpHeadAttribute";
        internal const string HttpPutAttribute = "HttpPutAttribute";
        internal const string HttpDeleteAttribute = "HttpDeleteAttribute";

        internal static string[] HttpMethods = new string[] { "GET", "POST", "HEAD", "PUT", "DELETE" };

        #region GetAssembly
        private static string _DllName;
        public static string DllName
        {
            get
            {
                if (string.IsNullOrEmpty(_DllName))
                {
                    _DllName = AppConfig.GetApp("Taurus.Controllers", "Taurus.Controllers");
                }
                return _DllName;
            }
        }
        private static List<Assembly> _Assemblys;
        public static List<Assembly> GetAssemblys()
        {
            if (_Assemblys == null)
            {
                string[] dllItems = DllName.Split(',');
                _Assemblys = new List<Assembly>(dllItems.Length);
                foreach (string item in dllItems)
                {
                    _Assemblys.Add(Assembly.Load(item)); // 可直接抛异常。
                }
                //try
                //{
                //_Assemblys = 
                //}
                //catch (Exception err)
                //{
                //    Log.WriteLogToTxt(err);
                //}
            }
            return _Assemblys;
        }
        //public static string GetClassFullName(string className)
        //{
        //    return DllName + "." + className;
        //}
        #endregion

        #region GetControllers

        private static Dictionary<string, Type> _Controllers = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private static readonly object objLock = new object();
        /// <summary>
        /// 获取控制器
        /// </summary>
        private static Dictionary<string, Type> GetControllers()
        {
            if (_Controllers.Count == 0)
            {
                lock (objLock)
                {
                    if (_Controllers.Count == 0)
                    {
                        List<Assembly> assList = GetAssemblys();
                        if (assList == null)
                        {
                            throw new Exception("Please make sure web.config'appSetting <add key=\"Taurus.Controllers\" value=\"YourControllerProjectName\") is right!");
                        }
                        foreach (Assembly ass in assList)
                        {
                            Type[] typeList = ass.GetExportedTypes();
                            foreach (Type type in typeList)
                            {
                                if (type.Name.EndsWith(Controller))
                                {
                                    if (type.BaseType != null && (type.BaseType.FullName == TaurusController || (type.BaseType.BaseType != null && type.BaseType.BaseType.FullName == TaurusController)))
                                    {
                                        _Controllers.Add(type.Name.Replace(Controller, ""), type);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return _Controllers;
        }
        /// <summary>
        /// 通过className类名获得对应的Controller类
        /// </summary>
        /// <returns></returns>
        public static Type GetType(string className)
        {
            Dictionary<string, Type> controllers = GetControllers();
            if (!string.IsNullOrEmpty(className) && controllers.ContainsKey(className)) //1：完整匹配【名称空间.类名】
            {
                return controllers[className];
            }
            if (controllers.ContainsKey(Default))
            {
                return controllers[Default];
            }
            return null;
        }

        #endregion

        #region GetMethods

        #region 3个全局方法


        private static MethodInfo _CheckTokenMethod = null;
        /// <summary>
        /// 全局CheckToken方法
        /// </summary>
        public static MethodInfo CheckTokenMethod
        {
            get
            {
                if (_CheckTokenMethod == null)
                {
                    Type t = GetType(DefaultController);
                    if (t != null)
                    {
                        _CheckTokenMethod = t.GetMethod(CheckToken, BindingFlags.Static | BindingFlags.Public);
                    }
                }
                return _CheckTokenMethod;
            }
        }
        private static MethodInfo _BeforeInvokeMethod = null;
        /// <summary>
        ///  全局BeforeInvoke方法
        /// </summary>
        public static MethodInfo BeforeInvokeMethod
        {
            get
            {
                if (_BeforeInvokeMethod == null)
                {
                    Type t = GetType(DefaultController);
                    if (t != null)
                    {
                        _BeforeInvokeMethod = t.GetMethod(BeforeInvoke, BindingFlags.Static | BindingFlags.Public);
                    }
                }
                return _BeforeInvokeMethod;
            }
        }

        private static MethodInfo _EndInvokeMethod = null;
        /// <summary>
        ///  全局EndInvokeMethod方法
        /// </summary>
        public static MethodInfo EndInvokeMethod
        {
            get
            {
                if (_EndInvokeMethod == null)
                {
                    Type t = GetType(DefaultController);
                    if (t != null)
                    {
                        _EndInvokeMethod = t.GetMethod(EndInvoke, BindingFlags.Static | BindingFlags.Public);
                    }
                }
                return _EndInvokeMethod;
            }
        }
        #endregion
        static Dictionary<string, Dictionary<string, MethodInfo>> typeMethods = new Dictionary<string, Dictionary<string, MethodInfo>>();
        static Dictionary<string, char[]> methodAttrs = new Dictionary<string, char[]>(StringComparer.OrdinalIgnoreCase);

        static readonly object methodObj = new object();
        internal static MethodInfo GetMethod(Type t, string methodName)
        {
            char[] hasTokenAttr;
            return GetMethod(t, methodName, out hasTokenAttr);
        }
        internal static MethodInfo GetMethod(Type t, string methodName, out char[] attrFlags)
        {
            string key = t.FullName;
            Dictionary<string, MethodInfo> dic = null;
            attrFlags = null;
            if (!typeMethods.ContainsKey(key))
            {
                lock (methodObj)
                {
                    if (!typeMethods.ContainsKey(key))
                    {
                        Type tokenType = typeof(TokenAttribute);
                        bool hasToken = t.GetCustomAttributes(tokenType, true).Length > 0;
                        if (hasToken)
                        {
                            methodAttrs.Add(key, null);
                        }
                        MethodInfo[] items = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                        dic = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
                        foreach (MethodInfo item in items)
                        {
                            if (!dic.ContainsKey(item.Name))//对于重载的同名方法，只取第一个空方法。
                            {
                                dic.Add(item.Name, item);//追加方法名
                                object[] attrs = item.GetCustomAttributes(true);
                                if (attrs.Length > 0)//追加特性名
                                {
                                    char[] aFlags = new char[6] { '0', '0', '0', '0', '0', '0' };
                                    foreach (object attr in attrs)
                                    {
                                        string[] names = attr.ToString().Split('.');
                                        switch (names[names.Length - 1])
                                        {
                                            case TokenAttribute:
                                                aFlags[0] = '1'; break;
                                            case HttpGetAttribute:
                                                aFlags[1] = '1'; break;
                                            case HttpPostAttribute:
                                                aFlags[2] = '1'; break;
                                            case HttpHeadAttribute:
                                                aFlags[3] = '1'; break;
                                            case HttpPutAttribute:
                                                aFlags[4] = '1'; break;
                                            case HttpDeleteAttribute:
                                                aFlags[5] = '1'; break;
                                        }

                                    }
                                    methodAttrs.Add(key + "." + item.Name, aFlags);
                                }
                            }
                        }
                        typeMethods.Add(key, dic);
                    }
                }
            }
            dic = typeMethods[key];
            if (!dic.ContainsKey(methodName))
            {
                methodName = Default;
            }
            if (methodAttrs.ContainsKey(key + "." + methodName))
            {
                attrFlags = methodAttrs[key + "." + methodName];
            }
            if (attrFlags == null)
            {
                attrFlags = new char[6] { '0', '0', '0', '0', '0', '0' };
            }
            if (methodAttrs.ContainsKey(key)) { attrFlags[0] = '1'; }
            if (dic.ContainsKey(methodName))
            {
                return dic[methodName];
            }
            return null;
        }
        #endregion
    }
}
