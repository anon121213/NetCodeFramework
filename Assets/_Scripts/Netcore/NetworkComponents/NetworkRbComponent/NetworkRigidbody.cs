using System.Reflection;
using Skynet.Data.Attributes;
using Skynet.NetworkComponents.NetworkTransformComponent;
using Skynet.NetworkComponents.RPCComponents;
using Skynet.RPCSystem;
using Skynet.RPCSystem.ProcessorsData;
using Skynet.Runner;
using UnityEngine;
using VContainer;

namespace Skynet.NetworkComponents.NetworkRbComponent
{
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkRigidbody : NetworkBehaviour
    {
        private readonly MethodInfo _methodInfoOnVelocityChange =
            typeof(NetworkRigidbody).GetMethod(nameof(OnVelocityChange));

        private readonly MethodInfo _methodInfoOnAngularVelocityChange =
            typeof(NetworkRigidbody).GetMethod(nameof(OnAngularVelocityChange));

        [SerializeField] private bool _enablePrediction = true;
        [SerializeField] private float _teleportThreshold = 2f;
        
        private INetworkRunner _networkRunner;
        private NetworkTransform _networkTransform;
        private Rigidbody _rb;

        private Vector3 _lastPosition;
        private Vector3 _lastVelocity;
        private Vector3 _lastAngularVelocity;

        [Inject]
        public void Initialize(INetworkRunner networkRunner)
        {
            _networkRunner = networkRunner;
            
            _rb = GetComponent<Rigidbody>(); 
            _networkTransform = GetComponent<NetworkTransform>(); 
            
            _lastPosition = _rb.position;
            _lastVelocity = _rb.linearVelocity;
            _lastAngularVelocity = _rb.angularVelocity;
            
            RPCInvoker.RegisterRPCInstance<NetworkRigidbody>(this);
        }

        private void FixedUpdate()
        {
            if (!_networkRunner.IsServer)
                return;

            if (Vector3.Distance(_rb.position, _lastPosition) > _teleportThreshold)
            {
                _networkTransform?.ForceSyncTransform();
                _lastPosition = _rb.position;
            }

            if (_rb.linearVelocity != _lastVelocity)
                InvokeVelocity();
            if (_rb.angularVelocity != _lastAngularVelocity)
                InvokeAngularVelocity();
        }

        private void InvokeVelocity()
        {
            RPCInvoker.InvokeBehaviourRPC<NetworkRigidbody>(this, _methodInfoOnVelocityChange,
                NetProtocolType.Udp, _rb.linearVelocity);

            _lastVelocity = _rb.linearVelocity;
        }

        private void InvokeAngularVelocity()
        {
            RPCInvoker.InvokeBehaviourRPC<NetworkRigidbody>(this, _methodInfoOnAngularVelocityChange,
                NetProtocolType.Udp, _rb.angularVelocity);

            _lastAngularVelocity = _rb.angularVelocity;
        }

        [ClientRPC]
        public void OnVelocityChange(Vector3 velocity)
        {
            _rb.linearVelocity = velocity;

            if (_enablePrediction)
                PredictMovement();
        }

        [ClientRPC]
        public void OnAngularVelocityChange(Vector3 angularVelocity) =>
            _rb.angularVelocity = angularVelocity;

        private void PredictMovement() =>
            _rb.position += _rb.linearVelocity * Time.fixedDeltaTime;
    }
}
