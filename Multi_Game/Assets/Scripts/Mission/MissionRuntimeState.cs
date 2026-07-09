using Fusion;

// 네트워크에 저장할 미션 상태
public struct MissionRuntimeState : INetworkStruct
{
    public int MissionId;
    public int Type;
    public PlayerRef Owner;

    public int Progress;
    public int RequiredProgress;

    public NetworkBool IsCompleted;
}