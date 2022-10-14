using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Facebook.WitAi.Samples.Shapes;

    public class WitController : MonoBehaviour
    {
        WitUIInteractionHandler witUI;
        // Start is called before the first frame update

        void Start()
        {
            witUI = this.GetComponent<WitUIInteractionHandler>();
        }

        public void __ActiveWitAgain()
        {
            StartCoroutine(WitActive());

        }
        IEnumerator WitActive()
        {
            yield return new WaitForSeconds(1f);
           // witUI.ToggleActivation();
        }
    }
