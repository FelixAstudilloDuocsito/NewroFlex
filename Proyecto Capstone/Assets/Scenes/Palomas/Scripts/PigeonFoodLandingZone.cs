using UnityEngine;

namespace NeuroFlexVR
{
    /// <summary>
    /// Alternativa XR al boton: pon este script en la zona con Collider Is Trigger.
    /// La comida debe llevar PigeonFoodItem + Rigidbody + Collider.
    /// Al entrar en la zona se inicia la llegada de las palomas.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PigeonFoodLandingZone : MonoBehaviour
    {
        public PigeonRoundController roundController;
        public bool consumeFoodOnHit = true;

        private void Reset()
        {
            Collider c = GetComponent<Collider>();
            c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (roundController == null || !roundController.IsAwaitingFood) return;
            PigeonFoodItem food = other.GetComponentInParent<PigeonFoodItem>();
            if (food == null) return;
            roundController.FoodLanded();
            if (consumeFoodOnHit) food.gameObject.SetActive(false);
        }
    }

}
