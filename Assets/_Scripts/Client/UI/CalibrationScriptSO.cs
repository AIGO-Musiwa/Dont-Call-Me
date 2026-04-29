using UnityEngine;

[CreateAssetMenu(fileName = "CalibrationScriptSO", menuName = "Don't Call Me/Calibration Script")]
public class CalibrationScriptSO : ScriptableObject
{
    [TextArea(2, 4)]
    public string[] sentences;

    public string GetRandom()
    {
        if (sentences == null || sentences.Length == 0)
            return "마이크에 대고 조용히 말해주세요.";

        return sentences[Random.Range(0, sentences.Length)];
    }
}
