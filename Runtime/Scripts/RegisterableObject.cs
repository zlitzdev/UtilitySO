using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using UnityEngine;

#if UNITY_EDITOR

using UnityEditor;

#endif

namespace Zlitz.General.UtilitySO
{
    public abstract class RegisterableObject<T, TId, TData> : ScriptableObject
        where T : RegisterableObject<T, TId, TData>
        where TData : class
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
                KeyValuePair<T, TData> content = new KeyValuePair<T, TData>(entry, entry.CreateData(entry));
            
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

        protected abstract TData CreateData(T entry);
    }

    public abstract class RegisterableObject<T, TId> : RegisterableObject<T, TId, VoidData>
        where T : RegisterableObject<T, TId>
    {
        protected override VoidData CreateData(T entry)
        {
            return null;
        }
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
            where TData : class
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

    #else
    
    internal static class RegistryImpl 
    {
        public static T[] GetEntries<T, TId, TData>()
            where T : RegisterableObject<T, TId, TData>
            where TData : class
        {
            return Resources.LoadAll<T>("").Where(e => e.includeInRegistry).ToArray();
        }
    }
    
    #endif
}
