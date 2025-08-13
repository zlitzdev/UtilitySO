using System;
using System.Collections.Generic;

using UnityEngine;

namespace Zlitz.General.UtilitySO
{
    public abstract class VariableObject<T> : EventObject<T>
    {
        [SerializeField]
        private T m_initialValue;

        private T m_value;

        public T value
        {
            get => m_value;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(m_value, value))
                {
                    if (m_value is IObservable observable1)
                    {
                        observable1.onChanged -= OnValueInternallyChanged;
                    }
                    m_value = value;
                    if (m_value is IObservable observable2)
                    {
                        observable2.onChanged += OnValueInternallyChanged;
                    }
                    Invoke(m_value);
                }
            }
        }

        protected override void OnInitialize()
        {
            m_value = m_initialValue;
            if (m_value is IObservable observable)
            {
                observable.onChanged += OnValueInternallyChanged;
            }

            base.OnInitialize();
        }

        protected override void OnListenerAdded(Action<T> listener)
        {
            listener?.Invoke(m_value);
        }
    
        private void OnValueInternallyChanged()
        {
            Invoke(m_value);
        }
    }

    public interface IObservable 
    {
        event Action onChanged;
    }
}
