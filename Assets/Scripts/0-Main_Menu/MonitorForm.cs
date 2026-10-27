using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MonitorForm : MonoBehaviour
{
    [SerializeField] private KeyCode openKey = KeyCode.F2;
    [SerializeField] private GameObject panel;

    [SerializeField] private ToggleGroup engagedGroup;
    [SerializeField] private Toggle engagedYes;
    [SerializeField] private ToggleGroup frustratedGroup;
    [SerializeField] private Toggle frustratedYes;
    [SerializeField] private Toggle[] improvementToggles;
    [SerializeField] private Toggle otherImprovementToggle;
    [SerializeField] private TMP_InputField otherImprovementField;
    [SerializeField] private TMP_InputField mistakesField;
    [SerializeField] private TMP_InputField finalReportField;
    [SerializeField] private TMP_Text errorText;

    void Update()
    {
        bool controlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (controlHeld && Input.GetKeyDown(openKey))
        {
            panel.SetActive(!panel.activeSelf);
            errorText.gameObject.SetActive(false);
        }
    }

    public void Submit()
    {
        bool anyImprovement = otherImprovementToggle.isOn;
        foreach (Toggle toggle in improvementToggles)
        {
            if (toggle.isOn) anyImprovement = true;
        }

        bool isValid = engagedGroup.AnyTogglesOn()
            && frustratedGroup.AnyTogglesOn()
            && anyImprovement
            && (!otherImprovementToggle.isOn || !string.IsNullOrWhiteSpace(otherImprovementField.text))
            && !string.IsNullOrWhiteSpace(mistakesField.text)
            && !string.IsNullOrWhiteSpace(finalReportField.text);

        errorText.gameObject.SetActive(!isValid);
        if (!isValid) return;

        List<string> improvements = new List<string>();
        foreach (Toggle toggle in improvementToggles)
        {
            if (toggle.isOn) improvements.Add(toggle.GetComponentInChildren<TMP_Text>().text);
        }

        GameEventReporter.Instance?.Report("monitor_rating", new MonitorRatingPayload
        {
            session_number = WebGLManager.Instance?.SessionNumber ?? 0,
            engaged = engagedYes.isOn,
            frustrated_wanted_to_stop = frustratedYes.isOn,
            improvements = improvements.ToArray(),
            other_improvement = otherImprovementToggle.isOn ? otherImprovementField.text : "",
            mistakes = mistakesField.text,
            final_report = finalReportField.text
        });

        ClearForm();
        panel.SetActive(false);
    }

    private void ClearForm()
    {
        engagedGroup.SetAllTogglesOff();
        frustratedGroup.SetAllTogglesOff();

        foreach (Toggle toggle in improvementToggles)
        {
            toggle.isOn = false;
        }
        otherImprovementToggle.isOn = false;

        otherImprovementField.text = "";
        mistakesField.text = "";
        finalReportField.text = "";
    }

    [System.Serializable]
    private class MonitorRatingPayload
    {
        public int session_number;
        public bool engaged;
        public bool frustrated_wanted_to_stop;
        public string[] improvements;
        public string other_improvement;
        public string mistakes;
        public string final_report;
    }
}
