using UnityEngine;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Dropdown speedDropdown;

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
}