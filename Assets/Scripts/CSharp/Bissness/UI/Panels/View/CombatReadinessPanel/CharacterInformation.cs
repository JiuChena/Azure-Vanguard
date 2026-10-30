using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CharacterInformation : MonoBehaviour
{
    public int chScale = 400;
    public TMP_Text chStarNumberText;
    public TMP_Text chLevelText;
    public TMP_Text chNameText;
    public Transform chLabelsTransform;
    //角色模型渲染层锚点（槽位节点下的 LayerInBGUI，模型挂这里才在信息 UI 下层）
    public Transform chModelAnchor;
}
