using UnityEngine;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    public const string MovementControlPrefKey = "movementControlMode";

    [Header("UI Elements")]
    public TMP_Dropdown speedDropdown;
    [Tooltip("Índice 0 = Mouse, índice 1 = Setas")]
    public TMP_Dropdown movementControlDropdown;

    void Start()
    {
        if (speedDropdown != null)
        {
            float currentSpeed = PlayerPrefs.GetFloat("treadmillSpeed", 5f);

            if (currentSpeed == 5f)
            {
                speedDropdown.value = 0;    // Default
            }
            else
            {
                speedDropdown.value = 1;    // Reduced
            }
        }

        if (movementControlDropdown != null)
        {
            movementControlDropdown.value = PlayerPrefs.GetInt(MovementControlPrefKey, 0);
        }
    }

    public void onDropdownValueChanged(int selectedIndex)
    {
        if (selectedIndex == 0)
        {
            PlayerPrefs.SetFloat("treadmillSpeed", 5f);
        }
        else if (selectedIndex == 1)
        {
            PlayerPrefs.SetFloat("treadmillSpeed", 2.5f);
        }

        PlayerPrefs.Save();
    }
    public void onMovementControlChanged(int selectedIndex)
    {
        PlayerPrefs.SetInt(MovementControlPrefKey, selectedIndex);
        PlayerPrefs.Save();
    }
}