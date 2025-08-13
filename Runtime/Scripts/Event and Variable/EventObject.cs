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
    public abstract class EventObject<T> : ScriptableObject
    {
        private static readonly HashSet<EventObject<T>> s_objects = new HashSet<EventObject<T>>();

        private Action<T> m_onInvoke;

        public void AddListener(Action<T> listener)
        {
            m_onInvoke += listener;
            OnListenerAdded(listener);
        }

        public void RemoveListener(Action<T> listener)
        {
            m_onInvoke -= listener;
        }

        public void Invoke(T eventData)
        {
            m_onInvoke?.Invoke(eventData);
        }

        private void OnEnable()
        {
            s_objects.Add(this);
        }

        private void OnDisable()
        {
            s_objects.Remove(this);
        }

        private void OnDestroy()
        {
            s_objects.Remove(this);
        }

        protected virtual void OnInitialize()
        {
            m_onInvoke = null;
        }

        protected virtual void OnListenerAdded(Action<T> listener)
        {
        }

        private static void Initialize()
        {
            foreach (EventObject<T> eventObject in s_objects)
            {
                eventObject.OnInitialize();
            }
        }
    }

    internal static class EventObjectInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Initialize()
        {
            HashSet<Type> genericRegisterableTypes = new HashSet<Type>();

            Type[] types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).ToArray();
            foreach (Type type in types)
            {
                Type baseType = type.BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    if (baseType.IsGenericType && !baseType.ContainsGenericParameters && baseType.GetGenericTypeDefinition() == typeof(EventObject<>))
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

    internal sealed class EventObjectModificationProcessor : AssetModificationProcessor
    {
        private static readonly Type s_genericEventObjectType = typeof(EventObject<>);

        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            ScriptableObject scriptableObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (IsEventObject(scriptableObject, out Type eventObjectType))
            {
                MethodInfo onDestroyMethod = eventObjectType?.GetMethod("OnDestroy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                onDestroyMethod?.Invoke(scriptableObject, null);
            }

            return AssetDeleteResult.DidNotDelete;
        }

        private static bool IsEventObject(ScriptableObject scriptableObject, out Type eventObjectType)
        {
            eventObjectType = null;
            if (scriptableObject == null)
            {
                return false;
            }

            Type type = scriptableObject.GetType();
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == s_genericEventObjectType)
                {
                    eventObjectType = type;
                    return true;
                }

                type = type.BaseType;
            }
            return false;
        }
    }

    #endif
}
