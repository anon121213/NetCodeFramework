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
        private readonly MethodInfo _methodInfo = typeof(NetworkTransform).GetMethod(nameof(OnTransformUpdate));

        [SerializeField] private bool _enableInterpolation = true;
        [SerializeField] private bool _enablePrediction = true;
        [SerializeField] private float _lerpSpeed = 10f;
        [SerializeField] private float _predictTime = 0.1f;
        [SerializeField] private float _teleportThreshold = 2f;

        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _lastScale;
        private INetworkRunner _networkRunner;

        [Inject]
        public void Initialize(INetworkRunner networkRunner)
        {
            _networkRunner = networkRunner;
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            _lastScale = transform.localScale;
            RPCInvoker.RegisterRPCInstance<NetworkTransform>(this);
        }

        private void LateUpdate()
        {
            if (!_networkRunner.IsServer)
                return;

            bool positionChanged = Vector3.Distance(transform.position, _lastPosition) > 0.01f;
            bool rotationChanged = Quaternion.Angle(transform.rotation, _lastRotation) > 0.1f;
            bool scaleChanged = transform.localScale != _lastScale;

            if (positionChanged || rotationChanged || scaleChanged)
            {
                RPCInvoker.InvokeBehaviourRPC<NetworkTransform>(this, _methodInfo,
                    NetProtocolType.Udp, transform.position, transform.rotation, transform.localScale);

                _lastPosition = transform.position;
                _lastRotation = transform.rotation;
                _lastScale = transform.localScale;
            }
        }

        [ClientRPC]
        public void OnTransformUpdate(Vector3 newPosition, Quaternion newRotation, Vector3 newScale)
        {
            if (Vector3.Distance(transform.position, newPosition) > _teleportThreshold)
            {
                transform.position = newPosition;
                transform.rotation = newRotation;
                transform.localScale = newScale;
            }
            else if (_enableInterpolation)
                InterpolateMovement(newPosition, newRotation, newScale);
            else if (_enablePrediction)
                PredictMovement(newPosition);
            else
            {
                transform.position = newPosition;
                transform.rotation = newRotation;
                transform.localScale = newScale;
            }

            _lastPosition = newPosition;
            _lastRotation = newRotation;
            _lastScale = newScale;
        }

        private void InterpolateMovement(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetScale)
        {
            float lerpFactor = 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, lerpFactor);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lerpFactor);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, lerpFactor);
        }

        private void PredictMovement(Vector3 targetPosition)
        {
            Vector3 predictedPosition = targetPosition + (targetPosition - _lastPosition) * _predictTime;
            transform.position = predictedPosition;
        }

        public void ForceSyncTransform()
        {
            RPCInvoker.InvokeBehaviourRPC<NetworkTransform>(this, _methodInfo,
                NetProtocolType.Udp, transform.position, transform.rotation, transform.localScale);
        }
    }
}
