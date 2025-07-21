using System;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using UnityEngine;

#if UNITY_EDITOR

using System.IO;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

#endif

namespace Zlitz.General.Registries
{
    public abstract class RegisterableObject<T, TId, TData> : ScriptableObject
        where T : RegisterableObject<T, TId, TData>
        where TData : class, new()
    {
        #region Registry

        private static Dictionary<TId, KeyValuePair<T, TData>> s_entries;

        private static void Initialize()
        {
            (s_entries ??= new Dictionary<TId, KeyValuePair<T, TData>>()).Clear();

            T[] entries = RegistryImpl.GetEntries<T, TId, TData>();
            foreach (T entry in entries)
            {
                TId id = entry.id;
                KeyValuePair<T, TData> content = new KeyValuePair<T, TData>(entry, new TData());
            
                if (!s_entries.TryAdd(id, content))
                {
                    Debug.LogWarning($"[{typeof(T).Name}] Duplicated ID detected: {id.ToString()}.");
                }
            }

        }

        public static IEnumerable<T> entries => s_entries.Values.Select(c => c.Key);

        public static IEnumerable<KeyValuePair<T, TData>> entriesWithData => s_entries.Values;

        public static T Get(TId id)
        {
            return Get(id, out TData data);
        }

        public static T Get(TId id, out TData data)
        {
            data = default;

            if (s_entries != null && id != null && s_entries.TryGetValue(id, out KeyValuePair<T, TData> content))
            {
                data = content.Value;
                return content.Key;
            }

            return null;
        }

        public bool TryGetData(out TData data)
        {
            if (Get(m_id, out data) == this)
            {
                return true;
            }

            data = default;
            return false;
        }

        [Serializable]
        public sealed class LazyReference
        {
            [SerializeField]
            private TId m_id;

            private T m_value;

            public T value => m_value == null ? (m_value = Get(m_id)) : m_value;
        }

        #endregion

        [Header("General")]

        [SerializeField]
        private TId m_id;

        public TId id => m_id;

        public virtual bool includeInRegistry => true;
    }

    public abstract class RegisterableObject<T, TId> : RegisterableObject<T, TId, VoidData>
        where T : RegisterableObject<T, TId>
    {
    }

    public sealed class VoidData
    {
    }

    internal static class RegistryInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Initialize()
        {
            HashSet<Type> genericRegisterableTypes = new HashSet<Type>();

            Type[] types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).ToArray();
            foreach (Type type in types)
            {
                Type baseType = type.BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    if (baseType.IsGenericType && !baseType.ContainsGenericParameters && baseType.GetGenericTypeDefinition() == typeof(RegisterableObject<,,>))
                    {
                        if (genericRegisterableTypes.Add(baseType))
                        {
                            MethodInfo initMethod = baseType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                            initMethod?.Invoke(null, null);
                        }
                        break;
                    }

                    baseType = baseType.BaseType;
                }
            }
        }
    }

    #if UNITY_EDITOR
    
    internal static class RegistryImpl 
    {
        public static T[] GetEntries<T, TId, TData>()
            where T : RegisterableObject<T, TId, TData>
            where TData : class, new()
        {
            HashSet<T> result = new HashSet<T>();

            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null || !asset.includeInRegistry)
                {
                    continue;
                }

                result.Add(asset);
            }

            return result.ToArray();
        }
    }

    internal class RegistryBuildProcess : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static readonly string s_resoucePath = "Assets/Resources/Temp_RegisterableObjects";

        int IOrderedCallback.callbackOrder => -200;

        void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
        {
            if (!Directory.Exists(s_resoucePath))
            {
                Directory.CreateDirectory(s_resoucePath);
            }

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

                Type assetType = asset.GetType();
                if (ShouldInclude(asset, assetType))
                {
                    string fileName = Path.GetFileName(assetPath);
                    string destPath = Path.Combine(s_resoucePath, fileName);
                    AssetDatabase.CopyAsset(assetPath, destPath);

                    copied++;
                    result.AppendLine($"\t{assetPath}");
                }
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

        private static readonly Type s_genericBaseType = typeof(RegisterableObject<,,>);

        private static bool ShouldInclude(object obj, Type type)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == s_genericBaseType)
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

    #else
    
    internal static class RegistryImpl 
    {
        public static T[] GetEntries<T, TId, TData>()
            where T : RegisterableObject<T, TId, TData>
            where TData : class, new()
        {
            return Resources.LoadAll<T>("").Where(e => e.includeInRegistry).ToArray();
        }
    }
    
    #endif
}
