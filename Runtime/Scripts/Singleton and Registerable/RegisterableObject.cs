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

        private TData m_registryData;

        private static Dictionary<TId, T> s_entries;

        private void InitializeData()
        {
            m_registryData = includeInRegistry ? CreateData() : null;
        }

        private static void Initialize()
        {
            (s_entries ??= new Dictionary<TId, T>()).Clear();

            T[] entries = RegistryImpl.GetEntries<T, TId, TData>();
            foreach (T entry in entries)
            {
                entry.InitializeData();
                if (!s_entries.TryAdd(entry.id, entry))
                {
                    Debug.LogWarning($"[{typeof(T).Name}] Duplicated ID detected: {entry.id.ToString()}.");
                }
            }

        }

        public static IEnumerable<T> entries => s_entries.Values;

        public static T Get(TId id)
        {
            return Get(id, out TData data);
        }

        public static T Get(TId id, out TData data)
        {
            data = default;

            if (s_entries != null && id != null && s_entries.TryGetValue(id, out T entry))
            {
                data = entry == null ? default : entry.m_registryData;
                return entry;
            }

            return null;
        }

        public bool TryGetData(out TData data)
        {
            if (this != null)
            {
                data = m_registryData;
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

            public override bool Equals(object obj)
            {
                if (obj is LazyReference other)
                {
                    return EqualityComparer<TId>.Default.Equals(m_id, other.m_id);
                }
                return false;
            }

            public override int GetHashCode()
            {
                return m_id?.GetHashCode() ?? 0;
            }
        }

        #endregion

        [Header("General")]

        [SerializeField]
        private TId m_id;

        public TId id => m_id;

        public virtual bool includeInRegistry => true;

        protected abstract TData CreateData();

        public override bool Equals(object other)
        {
            if (other == null)
            {
                return !includeInRegistry;
            }
            if (other is RegisterableObject<T, TId, TData> registerable)
            {
                if (!includeInRegistry)
                {
                    return !registerable.includeInRegistry;
                }
                return registerable == this;
            }
            return false;
        }

        public override int GetHashCode()
        {
            if (!includeInRegistry)
            {
                return 0;
            }
            return m_id?.GetHashCode() ?? 0;
        }

        public static bool operator ==(RegisterableObject<T, TId, TData> obj1, RegisterableObject<T, TId, TData> obj2)
        {
            ScriptableObject scriptable1 = obj1;
            ScriptableObject scriptable2 = obj2;

            if (scriptable1 == null || !obj1.includeInRegistry)
            {
                return scriptable2 == null || !obj2.includeInRegistry;
            }

            return scriptable2 == scriptable1;
        }

        public static bool operator !=(RegisterableObject<T, TId, TData> obj1, RegisterableObject<T, TId, TData> obj2)
        {
            ScriptableObject scriptable1 = obj1;
            ScriptableObject scriptable2 = obj2;

            if (scriptable1 == null || !obj1.includeInRegistry)
            {
                return scriptable2 != null && obj2.includeInRegistry;
            }

            return scriptable2 != scriptable1;
        }
    }

    public abstract class RegisterableObject<T, TId> : RegisterableObject<T, TId, VoidData>
        where T : RegisterableObject<T, TId>
    {
        protected override VoidData CreateData()
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
