using System;
using System.Collections.Generic;
using VRC.Playables;

namespace VibeGoesBrrr
{
  public class ExpressionParam<T> where T: unmanaged
  {
    public readonly string Name;
    public readonly VRCPlayer Player;
    
    private AvatarPlayableController PlayableController => Player?.field_Private_VRC_AnimationController_0?.field_Private_IkController_0?.field_Private_AvatarAnimParamController_0?.field_Private_AvatarPlayableController_0;
    private AvatarParameter mParameter;

    public static ExpressionParam<T> Find(string name, VRCPlayer player = null)
    {
      var param = new ExpressionParam<T>(name, player);
      return param.Valid ? param : null;
    }

    public ExpressionParam(string name, VRCPlayer player = null)
    {
      Name = name;
      Player = player ? player : Util.LocalPlayer;
      
      if (PlayableController != null) {
        foreach (var kv in PlayableController.Method_Public_Dictionary_2_Int32_AvatarParameter_0()) {
          if (kv.Value.prop_String_0 == Name) {
            mParameter = kv.Value;
            break;
          }
        }
      }
    }

    public bool Valid => mParameter != null;

    public T? Value
    {
      get {
        if (mParameter == null) {
          return null;
        }

        if (mParameter.field_Public_ParameterType_0 == AvatarParameter.ParameterType.Float) {
          return (T)(object)mParameter.prop_Single_0;
        } else if (mParameter.field_Public_ParameterType_0 == AvatarParameter.ParameterType.Int) {
          return (T)(object)mParameter.prop_Int32_1;
        } else if (mParameter.field_Public_ParameterType_0 == AvatarParameter.ParameterType.Bool) {
          return (T)(object)mParameter.prop_Boolean_0;
        } else {
          throw new System.ArgumentException();
        }
      }

      set {
        if (mParameter == null) {
          return;
        }

        if (mParameter.field_Public_ParameterType_0 == AvatarParameter.ParameterType.Float) {
          mParameter.prop_Single_0 = (float)(object)value;
        } else if (mParameter.field_Public_ParameterType_0 == AvatarParameter.ParameterType.Int) {
          mParameter.prop_Int32_1 = (int)(object)value;
        } else if (mParameter.field_Public_ParameterType_0 == AvatarParameter.ParameterType.Bool) {
          mParameter.prop_Boolean_1 = (bool)(object)value;
        } else {
          throw new System.ArgumentException();
        }
      }
    }

    public bool Prioritized {
      get {
        var index = GetIndex();
        if (index != -1) {
          return PlayableController.Method_Private_Boolean_Int32_0(index);
        } else {
          return false;
        }
      }

      set {
        var index = GetIndex();
        if (index != -1) {
          if (value) {
            PlayableController.AssignPuppetChannel(index);
          } else {
            PlayableController.ClearPuppetChannel(index);
          }
        }
      }
    }

    private int GetIndex()
    {
      var parameters = this.Player?.prop_VRCAvatarManager_0?.prop_VRCAvatarDescriptor_0?.expressionParameters?.parameters;
      if (parameters != null) {
        for (var i = 0; i < parameters.Length; i++) {
          if (parameters[i].name == this.Name) {
            return i;
          }
        }
      }
      return -1;
    }
  }
}