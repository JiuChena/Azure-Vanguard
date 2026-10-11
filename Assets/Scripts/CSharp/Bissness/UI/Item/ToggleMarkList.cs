using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleMarkList : MonoBehaviour
{
    public List<Image> markImageList = new List<Image>();

    public void ToggleSet(bool value)
    {
        foreach (Image image in markImageList)
        {
            image.gameObject.SetActive(value);
        }
    }
}
