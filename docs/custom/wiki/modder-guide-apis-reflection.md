> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Reflection)

# Modding:Modder Guide/APIs/Reflection (`Modder_Guide/APIs/Reflection`)

The reflection API lets you access fields, properties, or methods you otherwise couldn't access. You can use it from `helper.Reflection` in your entry method, or `this.Helper.Reflection` elsewhere in your entry class.

## Intro
[*Reflection*](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/reflection) is a powerful C# feature which lets code analyse and interact with code. SMAPI provides a simplified reflection API focused on accessing private game fields, properties, and methods. SMAPI will automatically handle validation, caching, and performance optimisation.

## Basic reflection
### Overview
SMAPI provides three overloaded methods to access code: `GetField`, `GetProperty`, and `GetMethod`. Each one takes three arguments:

| argument | purpose |
| --- | --- |
| `obj` or `type` | The instance (or type if static) which has the private field/property/method you want to access. |
| `name` | The name of the private field/property/method. |
| `required` | Whether to throw a descriptive exception if the field/property/method isn't found. Default true. If set to false, it will return `null` instead and you should validate it yourself. |

Each method returns an object you can use to interact further with it — like getting or setting the field/property value, or invoking the method.

### Fields and properties
`GetField` and `GetProperty` are used the same way. Both return an object with two methods: `GetValue` returns the current field/property value, and `SetValue` overrides their value.

You can get the value directly:

```c#
// get value of instance field
bool wasPet = this.Helper.Reflection.GetField<bool>(pet, "wasPetToday").GetValue();
```

Or you can keep a reference to the reflection data, and change the value separately:

```c#
// set value of static field
IReflectedField<int> soundTimer = this.Helper.Reflection.GetField<int>(typeof(Junimo), "soundTimer");
soundTimer.SetValue(100);
```

If you need to access the field/property repeatedly, keeping the reflection object will improve performance.

### Methods
`GetMethod` returns an object with one overloaded method. The `Invoke` comes in two forms:
- `Invoke()` calls a method with no return value.
- `Invoke<T>()` calls a method which returns a value, where `T` is the expected return type.

For example, this calls the private `TV.getFortuneForecast` method and stores the value it returns:

```c#
string forecast = this.Helper.Reflection
   .GetMethod(new TV(), "getFortuneForecast")
   .Invoke<string>();
```

If the method expects arguments, you can just add those to the `Invoke` method:

```c#
Vector2 spawnTile = new Vector2(25, 25);
this.helper.Reflection
   .GetMethod(Game1.getFarm(), "doSpawnCrow")
   .Invoke(spawnTile);
```

## Advanced reflection
If you need to do more, you can access the underlying C# reflection types:

```c#
FieldInfo field = this.Helper.Reflection.GetField<string>(…).FieldInfo;
MethodInfo method = this.Helper.Reflection.GetMethod(…).MethodInfo;
```

Or even use [C# reflection](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/reflection) directly. Note that SMAPI adds caching and optimisations which you'll need to handle yourself if you do this.

ru:Модификации:Руководство мододела/API/Рефлексия
zh:模组:制作指南/APIs/Reflection
