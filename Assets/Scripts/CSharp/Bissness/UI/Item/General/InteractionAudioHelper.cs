using System;
using System.Collections;
using System.Collections.Generic;
using Core.Gear;
using UnityEngine;
using UnityEngine.UI;
using AudioType = Core.Gear.AudioType;

public class InteractionAudioHelper : MonoBehaviour
{
    public AudioClip audioClip;
    public void PlayAudio()
    {
        if(audioClip != null) AudioManager.Instance.AudioPlay(audioClip, AudioType.Sound, null);
    }

    //Toggle 的 onValueChanged 为带参事件（UnityAction<bool>），持久化绑定需同形签名；参数仅兼容签名，忽略
    public void PlayAudio(bool isOn)
    {
        PlayAudio();
    }

    //Dropdown/TMP_Dropdown 的 onValueChanged 为带参事件（UnityAction<int>），持久化绑定需同形签名；参数仅兼容签名，忽略
    public void PlayAudio(int index)
    {
        PlayAudio();
    }
}
