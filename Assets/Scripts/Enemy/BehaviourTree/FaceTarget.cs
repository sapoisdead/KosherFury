using UnityEngine;

namespace BehaviourTree
{
    // Helper condiviso: ruota un nemico verso un bersaglio sul solo piano orizzontale
    // e riporta quanto e' ancora disallineato. Usato sia mentre ci si riposiziona sia
    // durante l'attacco, cosi' il comportamento e' identico nei due casi.
    public static class FaceTarget
    {
        // Ruota verso il bersaglio e ritorna l'angolo residuo in gradi (0 = perfettamente allineato)
        public static float Rotate(Transform self, Transform target, float rotationSpeed)
        {
            Vector3 dir = target.position - self.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f) return 0f;

            Quaternion desired = Quaternion.LookRotation(dir);
            self.rotation = Quaternion.Slerp(self.rotation, desired, Time.deltaTime * rotationSpeed);

            return Quaternion.Angle(self.rotation, desired);
        }

        // Angolo attuale verso il bersaglio, senza ruotare
        public static float AngleTo(Transform self, Transform target)
        {
            Vector3 dir = target.position - self.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f) return 0f;

            return Vector3.Angle(self.forward, dir);
        }
    }
}
