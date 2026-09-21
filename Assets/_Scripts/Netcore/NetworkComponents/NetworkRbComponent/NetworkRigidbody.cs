using Skynet.Data.Attributes;
using Skynet.NetworkComponents.RpcComponents;
using Skynet.Runner;
using UnityEngine;
using VContainer;

namespace Skynet.NetworkComponents
{
    [RequireComponent(typeof(Rigidbody))]
    public partial class NetworkRigidbody : NetworkBehaviour
    {
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

            // TODO(Chunk 4d, source generator): register RPC handlers via generated code.
        }

        private void FixedUpdate()
        {
            if (!_networkRunner.IsServer)
                return;

            if (Vector3.Distance(_rb.position, _lastPosition) > _teleportThreshold)
            {
                //_networkTransform?.ForceSyncTransform();
                _lastPosition = _rb.position;
            }

            if (_rb.linearVelocity != _lastVelocity)
                InvokeVelocity();
            if (_rb.angularVelocity != _lastAngularVelocity)
                InvokeAngularVelocity();
        }

        private void InvokeVelocity()
        {
            // TODO(Chunk 4d): generated sender — Client.OnVelocityChange(_rb.linearVelocity) over UDP.
            _lastVelocity = _rb.linearVelocity;
        }

        private void InvokeAngularVelocity()
        {
            // TODO(Chunk 4d): generated sender — Client.OnAngularVelocityChange(_rb.angularVelocity) over UDP.
            _lastAngularVelocity = _rb.angularVelocity;
        }

        [ClientRpc]
        public void OnVelocityChange(Vector3 velocity)
        {
            _rb.linearVelocity = velocity;

            if (_enablePrediction)
                PredictMovement();
        }

        [ClientRpc]
        public void OnAngularVelocityChange(Vector3 angularVelocity) =>
            _rb.angularVelocity = angularVelocity;

        private void PredictMovement() =>
            _rb.position += _rb.linearVelocity * Time.fixedDeltaTime;
    }
}
