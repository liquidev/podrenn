using Godot;
using System;
using System.Collections.Generic;

public class DebugDraw : Control
{
   private static DebugDraw instance = null;
   public static DebugDraw Instance
   {
      get { return instance; }
   }

   private List<DrawPoint> pointQueue = new List<DrawPoint>();
   private List<DrawVector> vectorQueue = new List<DrawVector>();

   public override void _Ready()
   {
      instance = this;
   }

   public override void _PhysicsProcess(float delta)
   {
      Update();
   }

   public override void _Draw()
   {
      var camera = GetViewport().GetCamera();

      foreach (var vector in vectorQueue)
      {
         var a = camera.UnprojectPosition(vector.origin);
         var b = camera.UnprojectPosition(vector.origin + vector.vector);
         DrawLine(a, b, vector.color);
      }

      foreach (var point in pointQueue)
      {
         var p = camera.UnprojectPosition(point.point);
         DrawCircle(p, 3f, point.color);
      }

      vectorQueue.Clear();
      pointQueue.Clear();
   }

   public void AddPoint(Vector3 point, Color color)
   {
      pointQueue.Add(new DrawPoint(point, color));
   }

   public static void Point(Vector3 point, Color color)
   {
      if (instance != null)
         instance.AddPoint(point, color);
   }

   public void AddVector(Vector3 origin, Vector3 vector, Color color)
   {
      vectorQueue.Add(new DrawVector(origin, vector, color));
   }

   public static void Vector(Vector3 origin, Vector3 vector, Color color)
   {
      if (instance != null)
         instance.AddVector(origin, vector, color);
   }

   private struct DrawPoint
   {
      public Vector3 point;
      public Color color;

      public DrawPoint(Vector3 point, Color color)
      {
         this.point = point;
         this.color = color;
      }
   }

   private struct DrawVector
   {
      public Vector3 origin;
      public Vector3 vector;
      public Color color;

      public DrawVector(Vector3 origin, Vector3 vector, Color color)
      {
         this.origin = origin;
         this.vector = vector;
         this.color = color;
      }
   }
}
