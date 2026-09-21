using System.Runtime.CompilerServices;

// Skynet.Unity is the Unity-side companion assembly that hosts MonoBehaviour framework
// primitives (NetworkBehaviour, NetworkTransform, PlayerLoopNetworkTickScheduler, etc.).
// It needs access to Skynet.Core's internal-marked framework subsystems on NetworkRunner
// (Transport, TickScheduler, ClockSync, Registry, Sender, Dispatcher) while those stay
// hidden from user gameplay code in Assembly-CSharp.
[assembly: InternalsVisibleTo("Skynet.Unity")]
