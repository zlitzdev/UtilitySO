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
    public abstract class SingletonObject<T> : ScriptableObject
        where T : SingletonObject<T>
    {
        public abstract int priority { get; }

        private static T s_instance;

        public static T instance => s_instance;
    
        private static void Initialize()
        {
            s_instance = SingletonImpl.Get<T>();
        }
    }

    internal static class SingletonInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            HashSet<Type> genericSingletonTypes = new HashSet<Type>();

            Type[] types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).ToArray();
            foreach (Type type in types)
            {
                Type baseType = type.BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    if (baseType.IsGenericType && !baseType.ContainsGenericParameters && baseType.GetGenericTypeDefinition() == typeof(SingletonObject<>))
                    {
                        if (genericSingletonTypes.Add(baseType))
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
    
    internal static class SingletonImpl
    {
        public static T Get<T>() 
            where T : SingletonObject<T>
        {
            T result = null;

            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                {
                    continue;
                }

                if (result == null || result.priority < asset.priority)
                {
                    result = asset;
                }
            }

            return result;
        }
    }

    #else
    
    internal static class SingletonImpl
    {
        public static T Get<T>()
            where T : SingletonObject<T>
        {
            return Resources.LoadAll<T>("").FirstOrDefault();
        }
    }
    
    #endif
}
