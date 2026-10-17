using UnityEngine;

namespace AEM.DigitalTwin
{
    public class Workpiece : MonoBehaviour
    {
        public GameObject rawBlank;
        public GameObject finishedPart;
        public bool IsFinished { get; private set; }

        public void MarkFinished()
        {
            IsFinished = true;
            rawBlank.SetActive(false);
            finishedPart.SetActive(true);
        }
    }
}
