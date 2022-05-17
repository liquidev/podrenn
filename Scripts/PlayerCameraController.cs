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
      var relativeCameraPosition = player.Transform.basis.Xform(new Vector3(0f, distance * 0.72f, -distance));
      var behindPlayer = player.Translation + relativeCameraPosition;

      var goingBackwards = player.Transform.basis.z.Dot(player.LinearVelocity);
      if (goingBackwards < 0.0f)
      {
         var xz = new Vector2(player.LinearVelocity.x, player.LinearVelocity.z);
         behindPlayer -= player.Transform.basis.z * xz.Length() * 0.15f;
      }
      camera.Translation = camera.Translation.LinearInterpolate(behindPlayer, 0.2f);

      var raycast = player.GetWorld().DirectSpaceState.IntersectRay(player.Translation, camera.Translation, new Godot.Collections.Array { player });
      if (raycast.Count > 0)
      {
         camera.Translation = (Vector3)raycast["position"] + (Vector3)raycast["normal"] * 0.05f;
      }
      var transform = camera.Transform;
      transform.basis = player.Transform.basis;
      camera.Transform = transform;

      camera.LookAt(player.Translation, player.Transform.basis.y);

      var velocity = player.LinearVelocity.Length();
      var fovCoeff = Mathf.Clamp(velocity / maxVelocity, 0.0f, 1.0f);
      var targetFov = Mathf.Lerp(minFov, maxFov, fovCoeff);
      fov = Mathf.Lerp(fov, targetFov, 0.2f);
   }
}
