using UnityEngine;

public class ControlsPanel : MonoBehaviour
{
    // Called by the Back button OnClick
    public void BackToPauseMenu()
    {
        if (PauseMenu.Instance != null)
        {
            PauseMenu.Instance.HideControls();
        }
    }
}
