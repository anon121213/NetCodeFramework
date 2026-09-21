using Skynet.Data.Attributes;
using Skynet.NetworkComponents.RpcComponents;
using Skynet.RpcSystem.ProcessorsData;
using Skynet.Snapshots;
using Skynet.Tick;
using Skynet.Unity.Snapshots;
using UnityEngine;
using VContainer;

namespace Skynet.NetworkComponents
{
    public partial class NetworkTransform : NetworkBehaviour
    {
        [SerializeField] private bool _enableInterpolation = true;
        [SerializeField] private bool _enablePrediction = true;
        [SerializeField] private bool _needToCollectNetworkObject = true;

        [SerializeField] private float _positionThreshold = 0.01f;
        [SerializeField] private float _rotationThreshold = 0.5f;
        [SerializeField] private float _scaleThreshold = 0.01f;
        [SerializeField] private float _teleportThreshold = 2f;
        [SerializeField] private uint _delayTicks = 2;
        [SerializeField] private float _keyframeIntervalTicks = 30;
        [SerializeField] private int _maxExtrapolationTicks = 2;

        [SerializeField] private NetworkObject _networkObject;

        private INetworkTickScheduler _tickScheduler;
        private IServerClockSync _clockSync;

        private SnapshotBuffer<TransformSnapshot> _buffer;
        private TransformSnapshot _lastReceived;

        private Vector3 _lastSentPos;
        private Quaternion _lastSentRot;
        private Vector3 _lastSentScale;
        
        private uint _lastKeyframeTick;
        private bool _needsKeyframe;

        [Inject]
        public void Construct(INetworkTickScheduler tickScheduler, IServerClockSync clockSync)
        {
            _tickScheduler = tickScheduler;
            _clockSync = clockSync;
        }

        private void Awake()
        {
            if (_needToCollectNetworkObject)
                _networkObject = GetComponent<NetworkObject>();
        }

        public override void OnNetworkReady()
        {
            _lastSentPos = transform.position;
            _lastSentRot = transform.rotation;
            _lastSentScale = transform.localScale;
            _buffer = new SnapshotBuffer<TransformSnapshot>(64);
            _needsKeyframe = true;
        }

        public override void OnNetworkUpdate(uint tick)
        {
            Debug.Log($"[NT.OnNetworkUpdate] tick={tick} isOwner={_networkObject.IsOwner} objId={_networkObject.NetworkObjectId}");
            if (!_networkObject.IsOwner)
                return;

            var positionDelta = transform.position - _lastSentPos;
            var rotationDelta = Quaternion.Angle(transform.rotation, _lastSentRot);
            var scaleDelta = transform.localScale - _lastSentScale;

            var posChanged = positionDelta.sqrMagnitude > _positionThreshold * _positionThreshold;
            var rotChanged = rotationDelta > _rotationThreshold;
            var scaleChanged = scaleDelta.sqrMagnitude > _scaleThreshold * _scaleThreshold;

            TransformSnapshotFlags flags = TransformSnapshotFlags.None;
            
            bool teleport = positionDelta.sqrMagnitude > _teleportThreshold * _teleportThreshold;
            bool keyframe = _needsKeyframe || (tick - _lastKeyframeTick >= _keyframeIntervalTicks);
            bool anyDelta = posChanged || rotChanged || scaleChanged;

            if (!keyframe && !anyDelta) return;
            
            if (keyframe || teleport)
            {
                flags = TransformSnapshotFlags.Position |
                        TransformSnapshotFlags.Rotation |
                        TransformSnapshotFlags.Scale;

                if (teleport) 
                    flags |= TransformSnapshotFlags.Teleport;
                
                _lastKeyframeTick = tick;
                _needsKeyframe = false;
            }
            else
            {
                if (posChanged) flags |= TransformSnapshotFlags.Position;
                if (rotChanged) flags |= TransformSnapshotFlags.Rotation;
                if (scaleChanged) flags |= TransformSnapshotFlags.Scale;
            }

            var snapshot = new TransformSnapshot(tick, flags, transform.position, transform.rotation,
                transform.localScale);

            if ((flags & TransformSnapshotFlags.Position) != 0) _lastSentPos = transform.position;
            if ((flags & TransformSnapshotFlags.Rotation) != 0) _lastSentRot = transform.rotation;
            if ((flags & TransformSnapshotFlags.Scale) != 0)    _lastSentScale = transform.localScale;
            
            Debug.Log($"[NT.SEND] flags={flags} pos={transform.position} lastSent={_lastSentPos}");
            SubmitOrApply(snapshot);
        }

        private void Update()
        {
            if (_networkObject.IsOwner) return;
            if (Time.timeScale <= 0f) return;
            if (!_clockSync.IsSynced) return;

            float renderTick = _clockSync.CurrentServerTick - _delayTicks;

            if (_buffer.TryGetStraddling(renderTick, out var older, out var newer, out var t))
            {
                if ((newer.Flags & TransformSnapshotFlags.Teleport) != 0)
                {
                    transform.position = newer.Position;
                    transform.rotation = newer.Rotation;
                    transform.localScale = newer.Scale;
                }
                else
                {
                    transform.position = Vector3.Lerp(older.Position, newer.Position, t);
                    transform.rotation = Quaternion.Slerp(older.Rotation, newer.Rotation, t);
                    transform.localScale = Vector3.Lerp(older.Scale, newer.Scale, t);
                }
            }
            else if (_buffer.TryGetLastTwo(out newer, out older) && renderTick > newer.Tick) {
                float overrun = renderTick - newer.Tick;
                if (overrun > _maxExtrapolationTicks) overrun = _maxExtrapolationTicks;
                Vector3 velocity = (newer.Position - older.Position) / ((newer.Tick - older.Tick) * _tickScheduler.TickInterval);
                transform.position = newer.Position + velocity * (overrun * _tickScheduler.TickInterval);
                transform.rotation = newer.Rotation;
                transform.localScale = newer.Scale;
            }
        }

        private TransformSnapshot Hydrate(TransformSnapshot incoming)
        {
            Vector3 pos = (incoming.Flags & TransformSnapshotFlags.Position) != 0
                ? incoming.Position
                : _lastReceived.Position;
            
            Quaternion rot = (incoming.Flags & TransformSnapshotFlags.Rotation) != 0
                ? incoming.Rotation
                : _lastReceived.Rotation;
            
            Vector3 scale = (incoming.Flags & TransformSnapshotFlags.Scale) != 0
                ? incoming.Scale 
                : _lastReceived.Scale;
            
            return new TransformSnapshot(incoming.Tick, incoming.Flags, pos, rot, scale);
        }

        private void SubmitOrApply(TransformSnapshot snapshot)
        {
            Debug.Log($"[NT.SubmitOrApply] IsServer={_networkObject.NetworkRunner.IsServer} sending flags={snapshot.Flags}");
            if (_networkObject.NetworkRunner.IsServer)
                ApplyTransform(snapshot);
            else
                SubmitTransform(snapshot);
        }

        [ServerRpc(NetProtocolType.Udp)]
        private void HandleSubmitTransform(TransformSnapshot snapshot)
        {
            ApplyTransform(snapshot);
        }

        [ClientRpc(NetProtocolType.Udp)]
        private void HandleApplyTransform(TransformSnapshot snapshot)
        {
            Debug.Log($"[NT.HandleApplyTransform] recv tick={snapshot.Tick} flags={snapshot.Flags} pos={snapshot.Position} isOwner={_networkObject.IsOwner}");
            if (_networkObject.IsOwner) return;
            var hydrated = Hydrate(snapshot);
            _buffer.Insert(hydrated);
            _lastReceived = hydrated;
        }
    }
}