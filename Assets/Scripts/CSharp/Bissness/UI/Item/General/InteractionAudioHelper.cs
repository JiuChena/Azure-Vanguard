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
        if(audioClip != null) AudioManager.Instance.AudioPlay(audioClip, AudioType.Sound, this.transform);
    }
}
