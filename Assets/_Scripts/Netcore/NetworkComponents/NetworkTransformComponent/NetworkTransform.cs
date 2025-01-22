using System.Reflection;
using _Scripts.Netcore.Data.Attributes;
using _Scripts.Netcore.NetworkComponents.RPCComponents;
using _Scripts.Netcore.RPCSystem;
using _Scripts.Netcore.RPCSystem.ProcessorsData;
using _Scripts.Netcore.Runner;
using UnityEngine;
using VContainer;

namespace _Scripts.Netcore.NetworkComponents.NetworkTransformComponent
{
    public class NetworkTransform : NetworkBehaviour
    {
        [SerializeField] private bool _enableInterpolation = true;
        [SerializeField] private bool _enablePrediction = true;
        
        [SerializeField] private float _lerpSpeed = 3f; 
        [SerializeField] private float _predictTime = 0.1f; 
        
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _lastScale;
        
        private MethodInfo _onPositionChangeMethod;
        private MethodInfo _onRotationChangeMethod;
        private MethodInfo _onScaleChangeMethod;
        private INetworkRunner _networkRunner;

        [Inject]
        public void Initialize(INetworkRunner networkRunner)
        {
            _networkRunner = networkRunner;
            _networkRunner.OnPlayerConnected += _ => InitServerPosition();
            
            _onPositionChangeMethod = typeof(NetworkTransform).GetMethod(nameof(OnPositionChange));
            _onRotationChangeMethod = typeof(NetworkTransform).GetMethod(nameof(OnRotationChange));
            _onScaleChangeMethod = typeof(NetworkTransform).GetMethod(nameof(OnScaleChange));

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            _lastScale = transform.localScale;
            
            RPCInvoker.RegisterRPCInstance<NetworkTransform>(this);
        }

        private void InitServerPosition()
        {
            InvokePosition();
            InvokeRotation();
            InvokeScale();
        }

        private void LateUpdate()
        {
            if(!_networkRunner.IsServer)
                return;

            if (transform.position != _lastPosition)
                InvokePosition();
            else if (transform.rotation != _lastRotation)
                InvokeRotation();
            else if (transform.localScale != _lastScale)
                InvokeScale();
        }

        private void InvokePosition()
        {
            RPCInvoker.InvokeBehaviourRPC<NetworkTransform>(this,
                _onPositionChangeMethod, NetProtocolType.Udp, transform.position);

            _lastPosition = transform.position;
        } 
        
        private void InvokeRotation()
        {
            RPCInvoker.InvokeBehaviourRPC<NetworkTransform>(this,
                _onRotationChangeMethod, NetProtocolType.Udp, transform.rotation);

            _lastRotation = transform.rotation;
        }
        
        private void InvokeScale()
        {
            RPCInvoker.InvokeBehaviourRPC<NetworkTransform>(this,
                _onScaleChangeMethod, NetProtocolType.Udp, transform.localScale);

            _lastScale = transform.localScale;
        }
        
        [ClientRPC]
        public void OnPositionChange(Vector3 newPosition)
        {
            transform.position = newPosition;

            if (_enableInterpolation)
                InterpolateMovement();
            else if (_enablePrediction)
                PredictMovement();
        }

        [ClientRPC]
        public void OnRotationChange(Quaternion newRotation)
        {
            transform.rotation = newRotation;
        }

        [ClientRPC]
        public void OnScaleChange(Vector3 newScale)
        {
            transform.localScale = newScale;
        }

        private void InterpolateMovement()
        {
            transform.position = Vector3.Lerp(transform.position, _lastPosition, Time.deltaTime * _lerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, _lastRotation, Time.deltaTime * _lerpSpeed);
            transform.localScale = Vector3.Lerp(transform.localScale, _lastScale, Time.deltaTime * _lerpSpeed);
        }

        private void PredictMovement()
        {
            Vector3 predictedPosition = transform.position + (transform.position - _lastPosition) * _predictTime;
            transform.position = predictedPosition;
        }
    }
}
