using System;
using System.Collections;
using System.Collections.Generic;
using Core.Gear;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ToggleHelper : MonoBehaviour
{
    private Animator animator;
    private Toggle toggle;

    private bool preState;

    [SerializeField] private string eventName;
    [SerializeField] private string eventParameter;

    //切换事件携带的参数（面板名），供外部读取做初始状态同步
    public string EventParameter => eventParameter;

    private void Start()
    {
        animator = GetComponent<Animator>();
        toggle = GetComponent<Toggle>();
        
        preState = toggle.isOn;
    }

    private void Update()
    {
        animator.SetBool("IsOn", toggle.isOn);

        if (toggle.isOn != preState)
        {
            preState = toggle.isOn;
            if(toggle.isOn) EventCenter.Instance.SetEventTrigger<string>(eventName, eventParameter);
        }
    }

    public void ToggleSelectEventRegister(UnityAction<string> callback)
    {
        EventCenter.Instance.Register<string>(eventName, callback);
    }
}
