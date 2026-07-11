using UnityEngine;

/// <summary>
/// LobbyScene에서 플레이어가 생성될 위치를 제공합니다.
///
/// 씬이 로드되면 지속 중인 FusionPlayerSpawner에
/// 자신을 등록하고 현재 접속 중인 플레이어 생성을 요청합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LobbyPlayerSpawnPoints : MonoBehaviour
{
    [Header("Spawn Points")]
    [Tooltip(
        "플레이어 생성 위치입니다. " +
        "배열 순서대로 사용됩니다."
    )]
    [SerializeField]
    private Transform[] spawnPoints;

    [Header("Fallback")]
    [Tooltip(
        "등록된 SpawnPoint보다 플레이어가 많을 때 " +
        "자동 생성 위치 사이의 간격입니다."
    )]
    [SerializeField, Min(0.5f)]
    private float fallbackSpacing = 2f;

    private FusionPlayerSpawner _playerSpawner;

    private void Start()
    {
        FusionNetworkManager networkManager =
            FusionNetworkManager.Instance;

        if (networkManager == null)
        {
            Debug.LogError(
                "[LobbyPlayerSpawnPoints] " +
                "FusionNetworkManager를 찾지 못했습니다.",
                this
            );

            enabled = false;
            return;
        }

        _playerSpawner =
            networkManager.PlayerSpawner;

        if (_playerSpawner == null)
        {
            Debug.LogError(
                "[LobbyPlayerSpawnPoints] " +
                "FusionPlayerSpawner가 생성되지 않았습니다.",
                this
            );

            enabled = false;
            return;
        }

        ValidateSpawnPoints();

        // 호스트 자신의 PlayerJoined 콜백이
        // LobbyScene 로드보다 먼저 실행됐을 수 있으므로
        // 씬이 준비된 시점에 현재 플레이어들을 다시 검사합니다.
        _playerSpawner.ActivateLobby(this);
    }

    private void OnDestroy()
    {
        if (_playerSpawner != null)
        {
            _playerSpawner.DeactivateLobby(this);
        }
    }

    /// <summary>
    /// 지정한 슬롯 번호에 해당하는 생성 위치와 회전을 반환합니다.
    ///
    /// 배열이 부족하거나 참조가 비어 있으면
    /// 자동으로 겹치지 않는 예비 위치를 계산합니다.
    /// </summary>
    public void GetSpawnPose(
        int slotIndex,
        out Vector3 position,
        out Quaternion rotation)
    {
        if (slotIndex >= 0 &&
            spawnPoints != null &&
            slotIndex < spawnPoints.Length)
        {
            Transform spawnPoint =
                spawnPoints[slotIndex];

            if (spawnPoint != null)
            {
                position = spawnPoint.position;
                rotation = spawnPoint.rotation;
                return;
            }
        }

        // SpawnPoint가 부족해도 같은 위치에 겹쳐 생성되지 않게 한다.
        position =
            transform.position +
            transform.right *
            (slotIndex * fallbackSpacing);

        rotation = transform.rotation;

        Debug.LogWarning(
            $"[LobbyPlayerSpawnPoints] " +
            $"SpawnPoint {slotIndex}가 없어 " +
            "예비 위치를 사용합니다.",
            this
        );
    }

    private void ValidateSpawnPoints()
    {
        if (spawnPoints == null ||
            spawnPoints.Length == 0)
        {
            Debug.LogWarning(
                "[LobbyPlayerSpawnPoints] " +
                "SpawnPoint가 하나도 등록되지 않았습니다. " +
                "자동 생성 위치를 사용합니다.",
                this
            );

            return;
        }

        for (int i = 0;
             i < spawnPoints.Length;
             i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogWarning(
                    $"[LobbyPlayerSpawnPoints] " +
                    $"Spawn Points 배열의 {i}번 참조가 비어 있습니다.",
                    this
                );
            }
        }
    }
}