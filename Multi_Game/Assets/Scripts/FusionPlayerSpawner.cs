using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// Host Mode에서 플레이어의 입장과 퇴장을 처리하고,
/// LobbyScene 로딩이 완료된 뒤 플레이어를 생성합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FusionPlayerSpawner :
    SimulationBehaviour,
    IPlayerJoined,
    IPlayerLeft,
    ISceneLoadStart,
    ISceneLoadDone
{
    private NetworkObject _playerPrefab;
    private LobbyPlayerSpawnPoints _spawnPoints;

    private bool _isLobbyReady;
    private bool _isSceneLoadComplete;

    private int _maxPlayers;

    // 각 플레이어가 사용하는 SpawnPoint 슬롯
    private readonly Dictionary<PlayerRef, int>
        _spawnSlotByPlayer = new();

    // 현재 사용할 수 있는 SpawnPoint 슬롯
    private readonly Stack<int>
        _availableSpawnSlots = new();

    // 비동기 생성 중인 플레이어
    // 중복 Spawn 요청을 방지합니다.
    private readonly HashSet<PlayerRef>
        _playersBeingSpawned = new();

    /// <summary>
    /// Runner가 시작되기 전에 한 번 호출해야 합니다.
    /// </summary>
    public void Initialize(
        NetworkObject playerPrefab,
        int maxPlayers)
    {
        if (playerPrefab == null)
        {
            throw new ArgumentNullException(
                nameof(playerPrefab),
                "플레이어 프리팹이 null입니다."
            );
        }

        if (maxPlayers < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPlayers),
                "최대 플레이어 수는 1 이상이어야 합니다."
            );
        }

        _playerPrefab = playerPrefab;
        _maxPlayers = maxPlayers;

        ResetSpawnSlots();
    }

    /// <summary>
    /// LobbyScene의 SpawnPoint 오브젝트가 준비되면 호출됩니다.
    /// </summary>
    public void ActivateLobby(
        LobbyPlayerSpawnPoints spawnPoints)
    {
        if (spawnPoints == null)
        {
            Debug.LogError(
                "[FusionPlayerSpawner] " +
                "LobbyPlayerSpawnPoints가 null입니다.",
                this
            );

            return;
        }

        _spawnPoints = spawnPoints;
        _isLobbyReady = true;

        /*
         * Fusion 씬 로딩 완료 콜백이 먼저 호출됐을 수도 있으므로
         * 여기서도 현재 플레이어 생성을 검사합니다.
         */
        TrySpawnAllPlayers();
    }

    /// <summary>
    /// LobbyScene이 제거될 때 이전 씬의 참조를 정리합니다.
    /// </summary>
    public void DeactivateLobby(
        LobbyPlayerSpawnPoints spawnPoints)
    {
        if (_spawnPoints != spawnPoints)
        {
            return;
        }

        _isLobbyReady = false;
        _spawnPoints = null;
    }

    /// <summary>
    /// Fusion 네트워크 씬 로딩이 시작될 때 호출됩니다.
    /// </summary>
    public void SceneLoadStart(SceneRef sceneRef)
    {
        _isSceneLoadComplete = false;
    }

    /// <summary>
    /// Fusion 네트워크 씬 로딩이 완전히 끝난 뒤 호출됩니다.
    /// </summary>
    public void SceneLoadDone(
        in SceneLoadDoneArgs sceneInfo)
    {
        _isSceneLoadComplete = true;

        /*
         * PlayerJoined가 씬 로딩보다 먼저 호출된 플레이어도
         * 여기서 다시 확인하여 생성합니다.
         */
        TrySpawnAllPlayers();
    }

    /// <summary>
    /// 플레이어가 방에 참가했을 때 호출됩니다.
    /// </summary>
    public void PlayerJoined(PlayerRef player)
    {
        TryBeginSpawn(player);
    }

    /// <summary>
    /// 플레이어가 방에서 나갔을 때 호출됩니다.
    /// </summary>
    public void PlayerLeft(PlayerRef player)
    {
        if (Runner == null ||
            !Runner.IsRunning ||
            !Runner.IsServer)
        {
            return;
        }

        if (Runner.TryGetPlayerObject(
                player,
                out NetworkObject playerObject))
        {
            if (playerObject != null &&
                Runner.Exists(playerObject))
            {
                Runner.Despawn(playerObject);
            }
        }

        ReleaseSpawnSlot(player);

        Debug.Log(
            $"[FusionPlayerSpawner] " +
            $"플레이어 퇴장: {player.RawEncoded}",
            this
        );
    }

    /// <summary>
    /// 현재 접속 중인 플레이어들을 모두 검사합니다.
    /// Lobby 활성화나 씬 로딩 완료 시 한 번만 실행됩니다.
    /// </summary>
    private void TrySpawnAllPlayers()
    {
        if (!CanSpawnPlayers())
        {
            return;
        }

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            TryBeginSpawn(player);
        }
    }

    /// <summary>
    /// 플레이어 한 명의 생성 작업을 시작합니다.
    /// </summary>
    private void TryBeginSpawn(PlayerRef player)
    {
        if (!CanSpawnPlayers())
        {
            return;
        }

        if (!Runner.IsPlayerValid(player))
        {
            Debug.LogWarning(
                $"[FusionPlayerSpawner] " +
                $"유효하지 않은 PlayerRef: " +
                $"{player.RawEncoded}",
                this
            );

            return;
        }

        // 이미 생성 작업이 진행 중이면 중복 요청을 무시합니다.
        if (_playersBeingSpawned.Contains(player))
        {
            return;
        }

        // 이미 플레이어 오브젝트가 있으면 다시 만들지 않습니다.
        if (Runner.TryGetPlayerObject(
                player,
                out NetworkObject existingObject) &&
            existingObject != null)
        {
            return;
        }

        // 이전 비정상 상태에서 슬롯만 남았다면 정리합니다.
        ReleaseSpawnSlot(player);

        if (_availableSpawnSlots.Count == 0)
        {
            Debug.LogError(
                "[FusionPlayerSpawner] " +
                "사용 가능한 SpawnPoint 슬롯이 없습니다.\n" +
                $"Max Players: {_maxPlayers}",
                this
            );

            return;
        }

        int spawnSlot =
            _availableSpawnSlots.Pop();

        _playersBeingSpawned.Add(player);

        /*
         * 이 메서드 내부에서 모든 예외를 처리하므로
         * fire-and-forget 호출이 안전합니다.
         */
        _ = SpawnPlayerAsync(
            player,
            spawnSlot
        );
    }

    /// <summary>
    /// 프리팹이 준비될 때까지 기다린 뒤 플레이어를 생성합니다.
    /// </summary>
    private async Task SpawnPlayerAsync(
        PlayerRef player,
        int spawnSlot)
    {
        bool slotWasAssigned = false;

        // 비동기 처리 중 필드가 바뀔 가능성을 줄이기 위해
        // 현재 Runner를 지역 변수로 보관합니다.
        NetworkRunner runner = Runner;

        try
        {
            if (_spawnPoints == null)
            {
                throw new InvalidOperationException(
                    "SpawnPoint 시스템이 존재하지 않습니다."
                );
            }

            _spawnPoints.GetSpawnPose(
                spawnSlot,
                out Vector3 spawnPosition,
                out Quaternion spawnRotation
            );

            /*
             * SpawnAsync는 프리팹 로딩이 지연되는 경우에도
             * 완료될 때까지 기다립니다.
             *
             * 성공하면 생성된 NetworkObject를 반환하고,
             * 실패하면 NetworkObjectSpawnException을 발생시킵니다.
             */
            NetworkObject spawnedPlayer =
                await runner.SpawnAsync(
                    _playerPrefab,
                    spawnPosition,
                    spawnRotation,
                    player
                );

            if (spawnedPlayer == null)
            {
                throw new InvalidOperationException(
                    "SpawnAsync가 null을 반환했습니다."
                );
            }

            /*
             * Spawn을 기다리는 동안 플레이어가 나가거나
             * LobbyScene이 제거됐을 수 있으므로 다시 검사합니다.
             */
            bool sessionIsStillValid =
                runner != null &&
                runner.IsRunning &&
                runner.IsServer;

            bool playerIsStillValid =
                sessionIsStillValid &&
                runner.IsPlayerValid(player);

            bool lobbyIsStillValid =
                _isLobbyReady &&
                _isSceneLoadComplete &&
                _spawnPoints != null;

            if (!playerIsStillValid ||
                !lobbyIsStillValid)
            {
                if (sessionIsStillValid &&
                    runner.Exists(spawnedPlayer))
                {
                    runner.Despawn(spawnedPlayer);
                }

                return;
            }

            /*
             * 비동기 Spawn 중 다른 시스템이 이미 플레이어를
             * 생성했는지도 마지막으로 확인합니다.
             */
            if (runner.TryGetPlayerObject(
                    player,
                    out NetworkObject existingObject) &&
                existingObject != null &&
                existingObject != spawnedPlayer)
            {
                if (runner.Exists(spawnedPlayer))
                {
                    runner.Despawn(spawnedPlayer);
                }

                return;
            }

            runner.SetPlayerObject(
                player,
                spawnedPlayer
            );

            _spawnSlotByPlayer[player] =
                spawnSlot;

            slotWasAssigned = true;

            Debug.Log(
                $"[FusionPlayerSpawner] " +
                $"플레이어 생성 성공\n" +
                $"Player: {player.RawEncoded}\n" +
                $"Spawn Slot: {spawnSlot}",
                spawnedPlayer
            );
        }
        catch (NetworkObjectSpawnException exception)
        {
            Debug.LogError(
                "[FusionPlayerSpawner] " +
                "NetworkObject 생성에 실패했습니다.\n" +
                $"Player: {player.RawEncoded}\n" +
                $"Prefab: " +
                $"{(_playerPrefab != null ? _playerPrefab.name : "null")}\n" +
                $"Exception: {exception.Message}",
                this
            );

            Debug.LogException(exception, this);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[FusionPlayerSpawner] " +
                "플레이어 생성 중 예외가 발생했습니다.\n" +
                $"Player: {player.RawEncoded}\n" +
                $"Exception: {exception.Message}",
                this
            );

            Debug.LogException(exception, this);
        }
        finally
        {
            _playersBeingSpawned.Remove(player);

            /*
             * 생성이 실패했거나 중간에 취소됐다면
             * 사용하려던 슬롯을 다시 반환합니다.
             */
            if (!slotWasAssigned)
            {
                _availableSpawnSlots.Push(
                    spawnSlot
                );
            }
        }
    }

    private bool CanSpawnPlayers()
    {
        return
            Runner != null &&
            Runner.IsRunning &&
            Runner.IsServer &&
            _playerPrefab != null &&
            _isLobbyReady &&
            _isSceneLoadComplete &&
            _spawnPoints != null;
    }

    private void ReleaseSpawnSlot(
        PlayerRef player)
    {
        if (!_spawnSlotByPlayer.TryGetValue(
                player,
                out int spawnSlot))
        {
            return;
        }

        _spawnSlotByPlayer.Remove(player);

        _availableSpawnSlots.Push(
            spawnSlot
        );
    }

    private void ResetSpawnSlots()
    {
        _spawnSlotByPlayer.Clear();
        _availableSpawnSlots.Clear();
        _playersBeingSpawned.Clear();

        /*
         * Stack에서 0번부터 꺼내지도록
         * 역순으로 넣습니다.
         */
        for (int i = _maxPlayers - 1;
             i >= 0;
             i--)
        {
            _availableSpawnSlots.Push(i);
        }
    }
}