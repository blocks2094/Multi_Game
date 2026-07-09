using Fusion;
using UnityEngine;

/// <summary>
/// 미션 배정, 진행도 저장, 완료처리, 시민 승리를 관리하는 스크립트
/// 
/// 게임 시작 시 공통 미션을 생성
/// 플레이어가 들어오면 미션을 배정
/// 모든 미션 상태를 NetworkArray로 동기화
/// 클라이언트가 미션 진행 요청을 보내면 Host가 승인
/// 미션 진행도를 올리고 완료 여부를 판단
/// 모든 미션이 완료되면 시민 승리 체크
/// </summary>


public class MissionSystem : NetworkBehaviour
{
    [SerializeField] private MissionDefinition[] commonMissions;
    [SerializeField] private MissionDefinition[] personalMissions;

    [Networked, Capacity(64)]
    public NetworkArray<MissionRuntimeState> Missions => default;

    [Networked]
    public int MissionCount { get; set; }

    [Networked]
    public NetworkBool IsInitialized { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority && !IsInitialized)
        {
            InitializeCommonMissions();
            IsInitialized = true;
        }
    }

    private void InitializeCommonMissions()
    {
        foreach (MissionDefinition def in commonMissions)
        {
            AddMission(def, PlayerRef.None);
        }
    }

    public void AssignPersonalMission(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (personalMissions.Length == 0)
            return;

        int index = player.RawEncoded % personalMissions.Length;
        MissionDefinition def = personalMissions[index];

        AddMission(def, player);
    }

    private void AddMission(MissionDefinition def, PlayerRef owner)
    {
        MissionRuntimeState state = new MissionRuntimeState
        {
            MissionId = def.missionId,
            Type = (int)def.missionType,
            Owner = owner,
            Progress = 0,
            RequiredProgress = def.requiredProgress,
            IsCompleted = false
        };

        Missions.Set(MissionCount, state);
        MissionCount++;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestMissionProgress(int missionId, RpcInfo info = default)
    {
        PlayerRef requester = info.Source;

        TryAddProgress(requester, missionId);
    }

    private void TryAddProgress(PlayerRef player, int missionId)
    {
        for (int i = 0; i < MissionCount; i++)
        {
            MissionRuntimeState state = Missions[i];

            if (state.MissionId != missionId)
                continue;

            if (state.IsCompleted)
                return;

            if ((MissionType)state.Type == MissionType.Personal && state.Owner != player)
                return;

            state.Progress++;

            if (state.Progress >= state.RequiredProgress)
            {
                state.Progress = state.RequiredProgress;
                state.IsCompleted = true;
            }

            Missions.Set(i, state);
            CheckCitizenWinCondition();
            return;
        }
    }

    private void CheckCitizenWinCondition()
    {
        for (int i = 0; i < MissionCount; i++)
        {
            MissionRuntimeState state = Missions[i];

            if (!state.IsCompleted)
                return;
        }

        Debug.Log("시민 승리!");
    }
}