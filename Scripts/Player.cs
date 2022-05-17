using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class Player : RigidBody
{
   [Export]
   public float maxAcceleration = 35000.0f;
   [Export]
   public float maxBraking = -16000.0f;
   [Export]
   public float accelerationRate = 100.0f;
   [Export]
   public float brakingRate = 200f;
   [Export]
   public float maxVelocity = 40.0f; // m/s
   [Export]
   public float steering = 0.05f;
   [Export]
   public float steeringAcceleration = 0.002f;
   [Export]
   public float steeringTiltAngle = 0.2f;
   [Export(PropertyHint.Range, "0,16")]
   public float hoverHeight = 1.0f;

   [Export]
   public bool debugProbeRaycasts;

   private Spatial[] hoverProbes;

   private Vehicle vehicle;

   private float height;
   private float thrust;
   private float gravityField;
   private float frictionCoeff;

   private Vector3 floorNormal;

   private float acceleration = 0.0f;

   private float angle = Mathf.Pi;
   private float steeringSpeed = 0.0f;

   public bool isBraking { get; private set; }

   private bool isNotSteering = false;
   private bool isSteeringLeft = false;
   private bool isSteeringRight = false;

   public override void _Ready()
   {
      hoverProbes = new Spatial[] {
         (Spatial)GetNode("Probes/FrontRight"),
         (Spatial)GetNode("Probes/FrontLeft"),
         (Spatial)GetNode("Probes/BackRight"),
         (Spatial)GetNode("Probes/BackLeft"),
      };
      vehicle = (Vehicle)GetNode("Vehicle");
      vehicle.TailLightsEnabled = false;
   }

   private Vector3 GetLocalDown()
   {
      return -Transform.basis.y;
   }

   private Vector2 GetHorizontalVelocity()
   {
      return new Vector2(LinearVelocity.x, LinearVelocity.z);
   }

   private ProbeResult? ProbeIntersection(Spatial probe, Vector3 direction, float maxDistance)
   {
      var origin = ToGlobal(probe.Translation);
      var dict = GetWorld().DirectSpaceState.IntersectRay(origin, origin + direction * maxDistance, new Godot.Collections.Array { this });
      if (dict.Count > 0)
      {
         var result = new ProbeResult(probe, (Vector3)dict["position"], (Vector3)dict["normal"]);
         if (debugProbeRaycasts)
         {
            var hitDistance = DistanceFromProbeToHit(result);
            DebugDraw.Vector(ToGlobal(probe.Translation), direction * hitDistance, new Color(0, 0, 1));
            DebugDraw.Point(result.hitPosition, new Color(1, 0, 0));
            DebugDraw.Vector(result.hitPosition, result.hitNormal * 0.5f, new Color(1, 0, 1));
         }
         return result;
      }
      else
      {
         return null;
      }
   }

   private List<ProbeResult> ProbeAllIntersections(float maxDistance)
   {
      var results = new List<ProbeResult>(4);
      foreach (var probe in hoverProbes)
      {
         if (ProbeIntersection(probe, GetLocalDown(), maxDistance) is ProbeResult intersection)
         {
            results.Add(intersection);
         }
      }
      return results;
   }

   private float DistanceFromProbeToHit(ProbeResult intersection)
   {
      return ToGlobal(intersection.probe.Translation).DistanceTo(intersection.hitPosition);
   }

   private ProbeResult? GetClosestIntersection(List<ProbeResult> intersections)
   {
      if (intersections.Count == 0)
         return null;
      var closest = intersections?[0];
      var closestDistance = float.NegativeInfinity;
      foreach (var intersection in intersections)
      {
         if (closest == null)
         {
            closest = intersection;
         }
         else
         {
            var distance = DistanceFromProbeToHit(intersection);
            if (distance < closestDistance)
            {
               closest = intersection;
               closestDistance = distance;
            }
         }
      }
      return closest;
   }

   private Vector3? GetAverageHitNormal(List<ProbeResult> intersections)
   {
      var normals = intersections.Select(intersection => intersection.hitNormal).ToArray();
      GD.Print(normals.Length);

      // NOTE: This is hardcoded to 4 normals because I can't figure out a way to take the average of
      // more than that in a sensible way.
      switch (normals.Length)
      {
         case 1:
            return normals[0];
         case 2:
            return Ligmath.Smid(normals[0], normals[1]);
         case 3:
            return Ligmath.Smid(Ligmath.Smid(normals[0], normals[1]), normals[2]);
         case 4:
            return Ligmath.Smid(Ligmath.Smid(normals[0], normals[1]), Ligmath.Smid(normals[2], normals[3]));
         default:
            return null;
      }
   }

   private void Hover()
   {
      var height = 100f;
      foreach (var intersection in ProbeAllIntersections(100f))
      {
         var intersectionHeight = DistanceFromProbeToHit(intersection);
         var inclineCosine = floorNormal.Dot(intersection.hitNormal);
         if (intersectionHeight < height && inclineCosine >= Mathf.Sqrt2 / 2f)
         {
            height = intersectionHeight;
         }
      }

      gravityField = Mathf.Clamp(height / hoverHeight - hoverHeight, 0.0f, 2.0f);
      GravityScale = gravityField;

      var yvelDamping = Math.Max(0.0f, hoverHeight - height - LinearVelocity.y * 0.15f) * Mass;
      ApplyCentralImpulse(floorNormal * yvelDamping);

      thrust = Mathf.Pow(Math.Max(0.0f, hoverHeight - height), 3.0f) * 5.0f;
      ApplyCentralImpulse(floorNormal * thrust * Mass);

      var frictionHeight = 6.0f;
      frictionCoeff = Math.Max(0.0f, (hoverHeight * frictionHeight - height) / (hoverHeight * frictionHeight));
      var frictionAmount = 10.0f;
      var frictionForce = -new Vector3(LinearVelocity.x, 0.0f, LinearVelocity.z) * frictionCoeff * frictionAmount;
      ApplyCentralImpulse(frictionForce);
   }

   private void UpdateFloorNormal()
   {
      var intersections = ProbeAllIntersections(2.5f);
      var targetNormal = GetAverageHitNormal(intersections);
      var closestIntersection = GetClosestIntersection(intersections);
      GD.Print(targetNormal);
      if (targetNormal is Vector3 normal && closestIntersection?.hitNormal is Vector3 closestNormal)
      {
         // No need to divide the dot product by the vectors' lengths because normals are
         // unit vectors.
         var inclineCosine = floorNormal.Dot(closestNormal);
         GD.Print(inclineCosine);
         // Limit the maximum incline you can climb to 45°.
         if (inclineCosine >= Mathf.Sqrt2 / 2f)
         {
            floorNormal = floorNormal.LinearInterpolate(normal, 0.1f);
         }
      }
      if (LinearVelocity.LengthSquared() < 0.0001)
      {
         floorNormal = Vector3.Up;
      }

      if (frictionCoeff < 0.1f)
      {
         floorNormal = floorNormal.LinearInterpolate(Vector3.Up, 0.01f);
      }
   }

   private void KeepUpright()
   {
      var rotated = Basis.Identity.Rotated(new Vector3(0, 1, 0), angle);
      var up = floorNormal;
      var right = Transform.basis.z.Cross(up);
      var forward = right.Cross(up);

      var transform = Transform;
      transform.basis = new Basis(right, up, forward).Orthonormalized() * rotated;

      var hVelocity = GetHorizontalVelocity().Length() / maxVelocity;
      var tilt = -steeringSpeed / steering * steeringTiltAngle * hVelocity * frictionCoeff;
      transform.basis = transform.basis.Rotated(transform.basis.z, tilt);

      Transform = transform;
   }

   private void LimitVelocity()
   {
      var hVelocity = GetHorizontalVelocity();
      var limitedSpeed = Math.Min(hVelocity.Length(), maxVelocity);
      hVelocity = hVelocity.Normalized() * limitedSpeed;
      var linVel = LinearVelocity;
      linVel.x = hVelocity.x;
      linVel.z = hVelocity.y;
      LinearVelocity = linVel;
   }

   public override void _IntegrateForces(PhysicsDirectBodyState state)
   {
      LimitVelocity();
      KeepUpright();
      Hover();
   }

   private void AccelerateAndBrake()
   {
      var forward = Transform.basis.z;
      forward.y = 0.0f;

      var wasBraking = isBraking;
      isBraking = false;

      var accelerating = false;
      var brakeDown = false;

      if (GetHorizontalVelocity().Length() <= 0.01f)
      {
         acceleration = 0f;
      }

      var forwardDirection = Transform.basis.z.Dot(LinearVelocity);

      if (Input.IsActionPressed("accelerate"))
      {
         acceleration += accelerationRate;
         accelerating = true;
      }
      if (Input.IsActionPressed("brake"))
      {
         acceleration += forwardDirection > 0 ? -brakingRate : -accelerationRate;
         accelerating = true;
         brakeDown = true;
      }
      if (!accelerating)
      {
         acceleration *= 0.975f;
      }
      acceleration = Mathf.Clamp(acceleration, maxBraking, maxAcceleration);
      if (frictionCoeff < 0.1f)
      {
         acceleration *= 0.995f;
      }

      AddCentralForce(forward * acceleration * Mathf.Clamp(frictionCoeff, 0.01f, 1.0f));

      if (forwardDirection > 0 && brakeDown)
      {
         isBraking = true;
      }
      // Kind of jank but needed to not unnecessarily send the signal every frame.
      if (!wasBraking && isBraking)
      {
         vehicle.TailLightsEnabled = true;
      }
      if (wasBraking && !isBraking)
      {
         vehicle.TailLightsEnabled = false;
      }
   }

   private void Steer()
   {
      var isSteering = false;
      if (Input.IsActionPressed("steer_left"))
      {
         steeringSpeed += steeringAcceleration;
         isSteering = true;
      }
      if (Input.IsActionPressed("steer_right"))
      {
         steeringSpeed -= steeringAcceleration;
         isSteering = true;
      }
      if (!isSteering)
      {
         steeringSpeed *= 0.9f;
      }
      steeringSpeed = Mathf.Clamp(steeringSpeed, -steering, steering);
      angle += steeringSpeed * Mathf.Clamp(frictionCoeff / 0.75f, 0.0f, 1.0f);

      var turningSteeringSpeed = steering * 0.75f;

      var wasSteeringLeft = isSteeringLeft;
      isSteeringLeft = steeringSpeed > turningSteeringSpeed;
      if (!wasSteeringLeft && isSteeringLeft)
      {
         vehicle.TurnSignal = Vehicle.TurnSignalMode.Left;
      }

      var wasSteeringRight = isSteeringRight;
      isSteeringRight = steeringSpeed < -turningSteeringSpeed;
      if (!wasSteeringRight && isSteeringRight)
      {
         vehicle.TurnSignal = Vehicle.TurnSignalMode.Right;
      }

      var wasNotSteering = isNotSteering;
      isNotSteering = steeringSpeed >= -turningSteeringSpeed && steeringSpeed <= turningSteeringSpeed;
      if (!wasNotSteering && isNotSteering)
      {
         vehicle.TurnSignal = Vehicle.TurnSignalMode.Off;
      }
   }

   public override void _PhysicsProcess(float delta)
   {
      UpdateFloorNormal();
      AccelerateAndBrake();
      Steer();
   }

   private struct ProbeResult
   {
      public Spatial probe;
      public Vector3 hitPosition;
      public Vector3 hitNormal;

      public ProbeResult(Spatial probe, Vector3 hitPosition, Vector3 hitNormal)
      {
         this.probe = probe;
         this.hitPosition = hitPosition;
         this.hitNormal = hitNormal;
      }
   }
}
