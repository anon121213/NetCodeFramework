using System;
using _Scripts.Netcore.NetworkComponents.NetworkVariableComponent.Processor;

namespace _Scripts.Netcore.NetworkComponents.NetworkVariableComponent
{
    public class NetworkVariable<T> : INetworkVariableRoot<T>
    {
        private readonly Action<string, T> _syncCallback;
        private readonly string _variableName;
        
        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                if (!NetworkVariableProcessor.Instance.TrySyncVariable(_variableName, value))
                    return;
                
                _value = value;
            }
        }
        
        public T ValueRoot
        {
            get => _value;
            set
            {
                _value = value;
                OnValueChanged?.Invoke(value);
            }
        }

        public event Action<T> OnValueChanged;

        public NetworkVariable(string variableName, T initialValue)
        {
            _variableName = variableName;
            _value = initialValue;
            
            NetworkVariableProcessor.Instance.RegisterNetworkVariable(variableName, this);
        }
    }

    public interface INetworkVariableRoot<T> : INetworkVariable<T>
    {
        T ValueRoot { get; set; }
    }

    public interface INetworkVariable<T>
    {
        T Value { get; set; }
        event Action<T> OnValueChanged;
    }
}