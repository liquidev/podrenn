using Godot;

public class Ligmath // Ligmath balls.
{
   public static Vector3 Smid(Vector3 a, Vector3 b)
   {
      if (a.DistanceSquaredTo(b) <= 0.00001f)
         return a;
      var om = a.AngleTo(b);
      var coeff = Mathf.Sin(0.5f * om) / Mathf.Sin(om);
      return coeff * (a + b);
   }
}
