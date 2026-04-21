using System.Data;
using UnityEngine;

public class ResultOverlayController : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private GameObject resultPanelRoot;
    [SerializeField] private ResultUI resultUI;

    // ── 외부 API ──────────────────────────────────────────

    // LobbyManager.Start()에서 ResultPlayload.Pending이 있을 때 호출
    public void Show(PendingResult payload)
    {
        resultUI.Setup(payload);
        resultPanelRoot.SetActive(true);

        SetLocalReviewing(true);

        ResultPayload.Pending = null;
    }

    // 결과화면에서 로비로 돌아가기 버튼 클릭시 실행
    public void OnReturnToLobbyClicked()
    {
        resultPanelRoot.SetActive(false);
        SetLocalReviewing(false);
    }

    // ── 내부 ──────────────────────────────────────────────

    private void SetLocalReviewing(bool value)
    {
        var runner = GameLauncher.Instance?.Runner;
        if (runner == null) return;

        var data = runner.GetPlayerObject(runner.LocalPlayer)?.GetComponent<PlayerData>();
        if (data == null || !data.HasInputAuthority) return;

        data.Rpc_SetReviewingResult(value);
    }
}
