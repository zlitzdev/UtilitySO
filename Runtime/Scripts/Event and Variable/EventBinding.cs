using System;

namespace Zlitz.General.UtilitySO
{
    public static class EventBinding
    {
        public static IEventBinding Create<T>(EventObject<T> eventObject, Action<T> callback)
        {
            return new Binding<T>(eventObject, callback);
        }

        private sealed class Binding<T> : IEventBinding
        {
            private EventObject<T> m_eventObject;
            private Action<T> m_callback;

            public Binding(EventObject<T> eventObject, Action<T> callback)
            {
                m_eventObject = eventObject;
                m_callback = callback;

                if (m_eventObject != null)
                {
                    m_eventObject.AddListener(Callback);
                }
            }

            private void Callback(T eventData)
            {
                m_callback?.Invoke(eventData);
            }

            void IEventBinding.Release()
            {
                if (m_eventObject != null)
                {
                    m_eventObject.RemoveListener(Callback);
                    m_eventObject = null;
                }
            }
        }
    }
}
