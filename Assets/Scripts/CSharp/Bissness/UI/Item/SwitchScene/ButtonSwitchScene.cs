using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonSwitchScene : MonoBehaviour
{
    public Button button;
    public string sceneName;
    
    private void Start()
    {
        button?.onClick.AddListener(OpenCombatReadinessPanel);
    }

    private void Update()
    {
        
    }

    private void OpenCombatReadinessPanel()
    {
        //打开战备面板，为战备面板写入此次战斗场景名称
    }
}
