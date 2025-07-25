#if UNITY_EDITOR

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Zlitz.General.UtilitySO
{
    internal class BuildProcess : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static readonly string s_resoucePath = "Assets/Resources/Temp";

        int IOrderedCallback.callbackOrder => -200;

        void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
        {
            if (!Directory.Exists(s_resoucePath))
            {
                Directory.CreateDirectory(s_resoucePath);
            }

            Dictionary<Type, KeyValuePair<ScriptableObject, int>> singletonObjects = new Dictionary<Type, KeyValuePair<ScriptableObject, int>>();

            int copied = 0;
            StringBuilder result = new StringBuilder()
                .AppendLine("Copied registerable objects:");

            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

                if (asset == null)
                {
                    continue;
                }

                if (IsIncludedRegisterable(asset))
                {
                    string fileName = Path.GetFileName(assetPath);
                    string destPath = Path.Combine(s_resoucePath, fileName);
                    AssetDatabase.CopyAsset(assetPath, destPath);

                    copied++;
                    result.AppendLine($"\t{assetPath}");
                }
                else if (IsSingleton(asset, out Type singletonType, out int priority))
                {
                    if (!singletonObjects.TryGetValue(singletonType, out KeyValuePair<ScriptableObject, int> currentSingleton))
                    {
                        singletonObjects.Add(singletonType, new KeyValuePair<ScriptableObject, int>(asset, priority));
                    }
                    else if (currentSingleton.Value < priority)
                    {
                        singletonObjects[singletonType] = new KeyValuePair<ScriptableObject, int>(asset, priority);
                    }
                }
            }

            foreach (KeyValuePair<ScriptableObject, int> singleton in singletonObjects.Values)
            {
                string assetPath = AssetDatabase.GetAssetPath(singleton.Key);
                string fileName = Path.GetFileName(assetPath);
                string destPath = Path.Combine(s_resoucePath, fileName);
                AssetDatabase.CopyAsset(assetPath, destPath);

                copied++;
                result.AppendLine($"\t{assetPath}");
            }

            result.AppendLine($"\tCopied assets: {copied}");
            Debug.Log(result.ToString());
        }

        void IPostprocessBuildWithReport.OnPostprocessBuild(BuildReport report)
        {
            if (Directory.Exists(s_resoucePath))
            {
                FileUtil.DeleteFileOrDirectory(s_resoucePath);
                FileUtil.DeleteFileOrDirectory(s_resoucePath + ".meta");
                AssetDatabase.Refresh();

                DeleteEmptyParentFolders(s_resoucePath);
            }
        }

        private static readonly Type s_genericRegisterableType = typeof(RegisterableObject<,,>);
        private static readonly Type s_genericSingletonType = typeof(SingletonObject<>);

        private static bool IsIncludedRegisterable(object obj)
        {
            Type type = obj.GetType();
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == s_genericRegisterableType)
                {
                    PropertyInfo prop = type.GetProperty("includeInRegistry", BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null || !prop.CanRead || prop.PropertyType != typeof(bool))
                    {
                        return false;
                    }

                    return (bool)prop.GetValue(obj);
                }

                type = type.BaseType;
            }
            return false;
        }

        private static bool IsSingleton(object obj, out Type singletonType, out int priority)
        {
            singletonType = null;
            priority = 0;

            Type type = obj.GetType();
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == s_genericSingletonType)
                {
                    PropertyInfo prop = type.GetProperty("priority", BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null || !prop.CanRead || prop.PropertyType != typeof(int))
                    {
                        return false;
                    }

                    singletonType = type;
                    priority = (int)prop.GetValue(obj);
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static void DeleteEmptyParentFolders(string startFolder)
        {
            string current = Path.GetDirectoryName(startFolder);

            while (!string.IsNullOrEmpty(current) && current.Replace('\\', '/').ToLower() != "assets")
            {
                if (Directory.Exists(current) && Directory.GetFileSystemEntries(current).Length == 0)
                {
                    FileUtil.DeleteFileOrDirectory(current);
                    FileUtil.DeleteFileOrDirectory(current + ".meta");
                }
                else
                {
                    break;
                }

                current = Path.GetDirectoryName(current);
            }

            AssetDatabase.Refresh();
        }
    }
}

#endif
