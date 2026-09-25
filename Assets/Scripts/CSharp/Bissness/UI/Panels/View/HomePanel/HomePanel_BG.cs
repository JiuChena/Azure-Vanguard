using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class HomePanel_BG : MonoBehaviour
{
    public VideoPlayer bgVideoPlayer;
    public RenderTexture bgRenderTexture;
    public RawImage bgRawImage;
    public AudioSource bgAudioSource;

    void Test()
    {
        int[] arr = Console.ReadLine().Split(' ').Select(int.Parse).ToArray();
    }
}
