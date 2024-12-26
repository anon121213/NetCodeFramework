using System;
using CodeBase.Network.NetworkComponents.NetworkVariableComponent.Processor;

namespace CodeBase.Network.NetworkComponents.NetworkVariableComponent
{
    public class NetworkVariable<T>
    {
        private T _value;
        private readonly Action<string, T> _syncCallback;
        private readonly string _variableName;

        public T Value
        {
            get => _value;
            set
            {
                if (Equals(_value, value))
                    return;

                _value = value;
                OnValueChanged?.Invoke(value);
                NetworkVariableProcessor.Instance.SyncVariable(_variableName, value);
            }
        }

        public event Action<T> OnValueChanged;

        public NetworkVariable(string variableName, T initialValue)
        {
            _variableName = variableName;
            _value = initialValue;
        }
    }
}