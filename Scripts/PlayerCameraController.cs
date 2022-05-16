using Godot;
using System;

public class PlayerCameraController : Node
{
   private Camera camera;
   private RigidBody player;

   float minFov = 75.0f;
   float maxFov = 80.0f;
   float maxVelocity = 60.0f;

   float fov;

   float distance = 2.0f;

   public override void _Ready()
   {
      fov = minFov;

      camera = (Camera)GetNode("Camera");
      player = (RigidBody)GetNode("Player");
   }

   public override void _PhysicsProcess(float delta)
   {
      var behindPlayer = player.Translation - player.Transform.basis.z * distance + new Vector3(0.0f, distance, 0.0f);
      var goingBackwards = player.Transform.basis.z.Dot(player.LinearVelocity);
      if (goingBackwards < 0.0f)
      {
         var xz = new Vector2(player.LinearVelocity.x, player.LinearVelocity.z);
         behindPlayer -= player.Transform.basis.z * xz.Length() * 0.15f;
      }
      camera.Translation = camera.Translation.LinearInterpolate(behindPlayer, 0.2f);
      camera.LookAt(player.Translation, Vector3.Up);

      var velocity = player.LinearVelocity.Length();
      var fovCoeff = Mathf.Clamp(velocity / maxVelocity, 0.0f, 1.0f);
      var targetFov = Mathf.Lerp(minFov, maxFov, fovCoeff);
      fov = Mathf.Lerp(fov, targetFov, 0.2f);
   }
}
