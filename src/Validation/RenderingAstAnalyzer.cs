using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SdvKb.Validation
{
    /// <summary>
    /// 基于 Roslyn C# AST 的 MonoGame / SMAPI 图形渲染静态体检器（`sdv-dev check` 编译前拦截）。
    ///
    /// 【当初为什么要写这个功能】：
    /// 在制作光影、水面反射、粒子特效与自定义 UI 模组时，单纯的 C# 语法编译和 Harmony 签名检查抓不到 MonoGame / SMAPI
    /// 渲染状态机层面的典型大坑，以往只能等进游戏刷红字或肉眼发现画面泛白/显存暴涨后回头排查：
    ///   1. `e.SpriteBatch.Begin()` 重复调用必崩（ERROR）：SMAPI 的 `Display.Rendered*` / `Rendering*` 事件触发时，
    ///      `Game1.spriteBatch` 内部已经处于活跃的 `Begin()` 状态；模组若未先 `End()` 就直接调 `Begin()`，当场抛
    ///      `InvalidOperationException` 刷屏报错；同理若 `End()` 了却没在退出前恢复 `Begin()`，原版后续收尾也会崩。
    ///   2. 渲染/帧循环内每帧 `new Texture2D` / `new RenderTarget2D` / `new Effect`（ERROR / WARN）：
    ///      在未加空值/尺寸缓存守卫（如 `if (_rt == null || _rt.Width != w)` 或 `??=`）的情况下，每帧分配非托管 GPU
    ///      资源会导致严重的显存泄漏与驱动卡顿。
    ///   3. 离屏 `SetRenderTarget` 未还原 &amp; 非预乘 Alpha `new Color(r, g, b, alpha)` 混用泛白（WARN）：
    ///      切到自定义离屏 `RenderTarget2D` 后若未通过 `SetRenderTargets` / `SetRenderTarget(null)` 还原会导致全屏黑屏或错位；
    ///      而程序化贴图或 `Draw` 染色使用 4 参数 `new Color(r, g, b, a)`（非预乘）搭配默认预乘 `BlendState.AlphaBlend`
    ///      会导致半透明边缘泛灰白。
    /// </summary>
    public static class RenderingAstAnalyzer
    {
        // SMAPI 所有在活跃 SpriteBatch.Begin 块内部触发的 Display 渲染事件名
        private static readonly HashSet<string> ActiveBatchDisplayEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            "Rendered", "RenderedWorld", "RenderedActiveMenu", "RenderedHud", "RenderedStep",
            "Rendering", "RenderingWorld", "RenderingActiveMenu", "RenderingHud", "RenderingStep"
        };

        // 对应的 SMAPI EventArgs 参数类型名（即使事件通过委托包装注册，也能直接靠参数类型精准识别渲染回调）
        private static readonly HashSet<string> ActiveBatchEventArgTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "RenderedEventArgs", "RenderedWorldEventArgs", "RenderedActiveMenuEventArgs", "RenderedHudEventArgs", "RenderedStepEventArgs",
            "RenderingEventArgs", "RenderingWorldEventArgs", "RenderingActiveMenuEventArgs", "RenderingHudEventArgs", "RenderingStepEventArgs"
        };

        private static readonly HashSet<string> PerFrameTickEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            "UpdateTicked", "UpdateTicking"
        };

        private static readonly HashSet<string> PerFrameTickEventArgTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "UpdateTickedEventArgs", "UpdateTickingEventArgs"
        };

        private static readonly HashSet<string> HeavyGpuResourceTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "Texture2D", "RenderTarget2D", "Effect", "BasicEffect", "AlphaTestEffect", "SpriteBatch"
        };

        private static readonly HashSet<string> StateObjectTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "BlendState", "SamplerState", "DepthStencilState", "RasterizerState"
        };

        public static void AnalyzeModRenderingPatterns(string modDir, string[] csFiles, List<ValidationIssue> issues)
        {
            if (csFiles == null || csFiles.Length == 0)
                return;

            string root = Path.GetFullPath(modDir);
            var parsedFiles = new List<(string RelPath, SyntaxTree Tree, CompilationUnitSyntax Root, string RawText)>();

            // 第一遍：解析所有源码 AST，并收集通过 `Display.Rendered* += Handler` 或 `GameLoop.UpdateTicked += Handler` 注册的方法名
            var activeBatchHandlerNames = new HashSet<string>(StringComparer.Ordinal);
            var perFrameHandlerNames = new HashSet<string>(StringComparer.Ordinal);
            bool projectUsesNonPremultiplied = false;

            foreach (var file in csFiles)
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch { continue; }

                if (text.Contains("NonPremultiplied", StringComparison.Ordinal))
                    projectUsesNonPremultiplied = true;

                string rel = file.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                    ? file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')
                    : Path.GetFileName(file);

                var tree = CSharpSyntaxTree.ParseText(text, path: rel);
                var cu = tree.GetCompilationUnitRoot();
                parsedFiles.Add((rel, tree, cu, text));

                foreach (var assign in cu.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    if (!assign.IsKind(SyntaxKind.AddAssignmentExpression))
                        continue;

                    string? eventMember = TryExtractLastMemberName(assign.Left);
                    if (string.IsNullOrEmpty(eventMember))
                        continue;

                    bool isDisplayBatch = ActiveBatchDisplayEvents.Contains(eventMember!);
                    bool isTick = PerFrameTickEvents.Contains(eventMember!);
                    if (!isDisplayBatch && !isTick)
                        continue;

                    // 右侧是方法组（如 `+= OnRenderedWorld` 或 `+= this.OnRenderedWorld`）
                    string? targetMethod = TryExtractLastMemberName(assign.Right);
                    if (!string.IsNullOrEmpty(targetMethod))
                    {
                        if (isDisplayBatch)
                        {
                            activeBatchHandlerNames.Add(targetMethod!);
                            perFrameHandlerNames.Add(targetMethod!);
                        }
                        if (isTick)
                        {
                            perFrameHandlerNames.Add(targetMethod!);
                        }
                    }

                    // 右侧如果是内联 Lambda（如 `Display.RenderedWorld += (s, e) => { ... }`），直接检查该 Lambda 体！
                    if (assign.Right is LambdaExpressionSyntax lambda)
                    {
                        string? evArgParam = TryGetSecondLambdaParamName(lambda);
                        if (isDisplayBatch)
                            CheckActiveSpriteBatchMisuse(lambda, evArgParam, rel, $"lambda({eventMember})", issues);
                        CheckPerFrameGpuAllocations(lambda, rel, $"lambda({eventMember})", issues);
                    }
                }
            }

            // 第二遍：逐文件逐方法执行 3 类渲染规则检查
            foreach (var (rel, _, cu, fileText) in parsedFiles)
            {
                bool fileMentionsNonPremultiplied = fileText.Contains("NonPremultiplied", StringComparison.Ordinal);

                foreach (var method in cu.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (method.Body == null && method.ExpressionBody == null)
                        continue;

                    string mName = method.Identifier.ValueText;
                    bool isActiveBatchMethod = activeBatchHandlerNames.Contains(mName) || HasParameterTypeIn(method, ActiveBatchEventArgTypes, out _);
                    HasParameterTypeIn(method, ActiveBatchEventArgTypes, out string? eventArgsParamName);

                    bool isPerFrameMethod = isActiveBatchMethod
                        || perFrameHandlerNames.Contains(mName)
                        || HasParameterTypeIn(method, PerFrameTickEventArgTypes, out _)
                        || IsLikelyDrawMethod(method);

                    // 规则 1：SMAPI Display.Rendered* / Rendering* 内重复 SpriteBatch.Begin 或未闭合恢复
                    if (isActiveBatchMethod)
                    {
                        CheckActiveSpriteBatchMisuse(method, eventArgsParamName, rel, mName, issues);
                    }

                    // 规则 2：渲染/Tick 高频回调内无缓存守卫的每帧 GPU 资源分配
                    if (isPerFrameMethod)
                    {
                        CheckPerFrameGpuAllocations(method, rel, mName, issues);
                    }

                    // 规则 3a：离屏 GraphicsDevice.SetRenderTarget(...) 未在同方法或 finally 中还原
                    CheckUnrestoredRenderTarget(method, cu, rel, mName, issues);

                    // 规则 3b：非预乘 `new Color(r, g, b, alpha)` 搭配默认预乘 AlphaBlend 泛白警告
                    if (!fileMentionsNonPremultiplied && !projectUsesNonPremultiplied)
                    {
                        CheckNonPremultipliedColorHazard(method, rel, mName, issues);
                    }
                }
            }
        }

        private static void CheckActiveSpriteBatchMisuse(
            SyntaxNode scopeNode,
            string? eventArgsParamName,
            string relFile,
            string memberLabel,
            List<ValidationIssue> issues)
        {
            // 按语法树出现顺序扫描对共享批次（`e.SpriteBatch` 或 `Game1.spriteBatch`）的 Begin / End 调用；
            // 注意排除方法内部自建的局部 `using var sb = new SpriteBatch(gd); sb.Begin(...)`！
            int sharedBatchDepth = 1; // 进入 SMAPI Display 渲染事件时，共享 SpriteBatch 已经处于 Begin (depth = 1) 状态
            bool reportedDoubleBegin = false;
            SyntaxNode? firstCallSite = null;

            foreach (var inv in scopeNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (inv.Expression is not MemberAccessExpressionSyntax ma)
                    continue;

                string callName = ma.Name.Identifier.ValueText;
                if (callName != "Begin" && callName != "End")
                    continue;

                if (!IsSharedSmapiSpriteBatchExpr(ma.Expression, eventArgsParamName))
                    continue;

                firstCallSite ??= inv;
                if (callName == "Begin")
                {
                    if (sharedBatchDepth >= 1 && !reportedDoubleBegin)
                    {
                        reportedDoubleBegin = true;
                        int line = GetLineNumber(inv);
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = $"{relFile}:{line}",
                            Message = $"SMAPI rendering crash in `{memberLabel}`: `{ma.Expression}.Begin(...)` is called while SMAPI's shared `SpriteBatch` is already active! Call `{ma.Expression}.End()` before starting a custom `Begin(...)` pass, and call `Begin(...)` again before returning."
                        });
                    }
                    sharedBatchDepth = 1;
                }
                else if (callName == "End")
                {
                    sharedBatchDepth = Math.Max(0, sharedBatchDepth - 1);
                }
            }

            // 如果回调里调了 e.SpriteBatch.End() 切批次，但退出前忘记调回 Begin()，SMAPI 事件结束收尾调 End() 时同样必崩！
            if (!reportedDoubleBegin && firstCallSite != null && sharedBatchDepth == 0)
            {
                int line = GetLineNumber(firstCallSite);
                issues.Add(new ValidationIssue
                {
                    Severity = "WARN",
                    File = $"{relFile}:{line}",
                    Message = $"Unbalanced shared `SpriteBatch` state in `{memberLabel}`: `End()` was called on SMAPI's `SpriteBatch` without restoring an active `Begin(...)` before returning (vanilla/SMAPI post-render `End()` may throw `InvalidOperationException`)."
                });
            }
        }

        private static bool IsSharedSmapiSpriteBatchExpr(ExpressionSyntax expr, string? eventArgsParamName)
        {
            string raw = expr.ToString().Replace(" ", "");
            if (raw.EndsWith(".SpriteBatch", StringComparison.Ordinal) ||
                raw.Equals("Game1.spriteBatch", StringComparison.Ordinal) ||
                raw.EndsWith(".Game1.spriteBatch", StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(eventArgsParamName) &&
                raw.Equals(eventArgsParamName + ".SpriteBatch", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static void CheckPerFrameGpuAllocations(
            SyntaxNode scopeNode,
            string relFile,
            string memberLabel,
            List<ValidationIssue> issues)
        {
            foreach (var creation in scopeNode.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                // 跳过嵌套局部函数或延迟执行的非立即回调（避免误报注册闭包）
                if (IsInsideNestedLambdaOrLocalFunction(creation, scopeNode))
                    continue;

                string typeName = ExtractSimpleTypeName(creation.Type);
                bool isHeavyGpu = HeavyGpuResourceTypes.Contains(typeName);
                bool isStateObj = !isHeavyGpu && StateObjectTypes.Contains(typeName);
                if (!isHeavyGpu && !isStateObj)
                    continue;

                // 排除合法的懒加载 / 分辨率变化重建模式：
                //   `_rt ??= new RenderTarget2D(...)`
                //   `if (_rt == null || _rt.IsDisposed || _rt.Width != w) { _rt = new RenderTarget2D(...); }`
                if (IsGuardedLazyFieldCache(creation, scopeNode))
                    continue;

                int line = GetLineNumber(creation);
                if (isHeavyGpu)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = $"{relFile}:{line}",
                        Message = $"Per-frame GPU resource leak/stall in `{memberLabel}`: `new {typeName}(...)` is allocated inside a render/tick callback without a field cache guard (`if (_cached == null || _cached.IsDisposed)`). Cache `{typeName}` in an instance field and reuse it across frames."
                    });
                }
                else
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "WARN",
                        File = $"{relFile}:{line}",
                        Message = $"Per-frame graphics state allocation in `{memberLabel}`: `new {typeName}(...)` runs every frame. Cache custom `{typeName}` in a `static readonly` or instance field."
                    });
                }
            }
        }

        private static bool IsGuardedLazyFieldCache(ObjectCreationExpressionSyntax creation, SyntaxNode methodScope)
        {
            // Case 1: `_tex ??= new Texture2D(...)`
            var parentAssign = creation.Ancestors().TakeWhile(n => n != methodScope).OfType<AssignmentExpressionSyntax>().FirstOrDefault();
            if (parentAssign != null && parentAssign.IsKind(SyntaxKind.CoalesceAssignmentExpression))
                return true;

            // Case 2: 赋值给局部变量（`var rt = new RenderTarget2D(...)` 或 `using var tex = new Texture2D(...)`）一定不是跨帧缓存！
            var localVarDecl = creation.Ancestors().TakeWhile(n => n != methodScope).OfType<LocalDeclarationStatementSyntax>().FirstOrDefault();
            if (localVarDecl != null)
            {
                // 除非该局部变量紧接着存入字典缓存且处于 TryGetValue / ContainsKey 分支中
                var enclosingIfForDict = creation.Ancestors().TakeWhile(n => n != methodScope).OfType<IfStatementSyntax>().FirstOrDefault();
                if (enclosingIfForDict != null)
                {
                    string cond = enclosingIfForDict.Condition.ToString();
                    if (cond.Contains("TryGetValue", StringComparison.Ordinal) || cond.Contains("ContainsKey", StringComparison.Ordinal))
                        return true;
                }
                return false;
            }

            // Case 3: 赋值给字段/属性/字典索引，且外层有 `if (... == null || ... IsDisposed || ... Width != ...)` 守卫
            foreach (var ifStmt in creation.Ancestors().TakeWhile(n => n != methodScope).OfType<IfStatementSyntax>())
            {
                string cond = ifStmt.Condition.ToString();
                if (cond.Contains("null", StringComparison.Ordinal) ||
                    cond.Contains("IsDisposed", StringComparison.Ordinal) ||
                    cond.Contains("Width", StringComparison.Ordinal) ||
                    cond.Contains("Height", StringComparison.Ordinal) ||
                    cond.Contains("TryGetValue", StringComparison.Ordinal) ||
                    cond.Contains("ContainsKey", StringComparison.Ordinal) ||
                    cond.Contains("!", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CheckUnrestoredRenderTarget(
            MethodDeclarationSyntax method,
            CompilationUnitSyntax cu,
            string relFile,
            string memberLabel,
            List<ValidationIssue> issues)
        {
            InvocationExpressionSyntax? customSetCall = null;
            bool hasRestoreInMethod = false;

            foreach (var inv in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (inv.Expression is not MemberAccessExpressionSyntax ma)
                    continue;

                string name = ma.Name.Identifier.ValueText;
                if (name == "SetRenderTargets")
                {
                    hasRestoreInMethod = true;
                }
                else if (name == "SetRenderTarget")
                {
                    var args = inv.ArgumentList.Arguments;
                    if (args.Count == 1 && args[0].Expression.IsKind(SyntaxKind.NullLiteralExpression))
                    {
                        hasRestoreInMethod = true;
                    }
                    else if (args.Count >= 1)
                    {
                        // 如果参数名本身就是 prev / old / original / backBuffer，也视为还原调用
                        string argText = args[0].Expression.ToString();
                        if (argText.IndexOf("prev", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            argText.IndexOf("orig", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            argText.IndexOf("old", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            argText.IndexOf("screen", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hasRestoreInMethod = true;
                        }
                        else
                        {
                            customSetCall ??= inv;
                        }
                    }
                }
            }

            if (customSetCall != null && !hasRestoreInMethod)
            {
                // 若文件内其他辅助方法（如 EndOffscreenPass / RestoreRenderTarget）里有 SetRenderTarget(null)/SetRenderTargets，检查当前方法是否调用了辅助方法
                bool fileHasHelperRestore = cu.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i =>
                    i.Expression is MemberAccessExpressionSyntax m &&
                    (m.Name.Identifier.ValueText == "SetRenderTargets" ||
                     (m.Name.Identifier.ValueText == "SetRenderTarget" &&
                      i.ArgumentList.Arguments.Count == 1 &&
                      i.ArgumentList.Arguments[0].Expression.IsKind(SyntaxKind.NullLiteralExpression))));

                if (!fileHasHelperRestore)
                {
                    int line = GetLineNumber(customSetCall);
                    issues.Add(new ValidationIssue
                    {
                        Severity = "WARN",
                        File = $"{relFile}:{line}",
                        Message = $"Offscreen `SetRenderTarget` state leak risk in `{memberLabel}`: `SetRenderTarget(...)` was called without restoring previous targets (`var prev = gd.GetRenderTargets(); try {{ ... }} finally {{ gd.SetRenderTargets(prev); }}`)."
                    });
                }
            }
        }

        private static void CheckNonPremultipliedColorHazard(
            MethodDeclarationSyntax method,
            string relFile,
            string memberLabel,
            List<ValidationIssue> issues)
        {
            foreach (var creation in method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                string typeName = ExtractSimpleTypeName(creation.Type);
                if (typeName != "Color")
                    continue;

                var args = creation.ArgumentList?.Arguments;
                if (args == null || args.Value.Count != 4)
                    continue;

                string alphaExpr = args.Value[3].Expression.ToString().Trim();
                // 完全不透明的常量 Alpha (255 / 1f / 1.0f / 1.0 / byte.MaxValue) 在预乘与非预乘下等价，不会泛白
                if (alphaExpr == "255" || alphaExpr == "1f" || alphaExpr == "1.0f" || alphaExpr == "1" ||
                    alphaExpr == "1.0" || alphaExpr == "byte.MaxValue")
                {
                    continue;
                }

                // 全 0 透明黑 `new Color(0, 0, 0, 0)` 也是预乘安全的（RGB 本来就是 0）
                if (args.Value[0].Expression.ToString().Trim() == "0" &&
                    args.Value[1].Expression.ToString().Trim() == "0" &&
                    args.Value[2].Expression.ToString().Trim() == "0")
                {
                    continue;
                }

                // 若 RGB 参数本身已经写成了乘以 alpha 的预乘表达式（如 `new Color(r * a, g * a, b * a, a)`），同样不误报！
                if (args.Value[0].Expression.ToString().Contains("*", StringComparison.Ordinal) &&
                    args.Value[1].Expression.ToString().Contains("*", StringComparison.Ordinal) &&
                    args.Value[2].Expression.ToString().Contains("*", StringComparison.Ordinal))
                {
                    continue;
                }

                int line = GetLineNumber(creation);
                issues.Add(new ValidationIssue
                {
                    Severity = "WARN",
                    File = $"{relFile}:{line}",
                    Message = $"Premultiplied alpha hazard in `{memberLabel}`: 4-arg `new Color(r, g, b, {alphaExpr})` creates non-premultiplied RGB values, which cause washed-out grey/white halos under MonoGame's default `BlendState.AlphaBlend`. Use `Color.FromNonPremultiplied(r, g, b, a)`, `new Color(r, g, b) * opacity`, or `BlendState.NonPremultiplied`."
                });
                break; // 每个方法最多提示一次，避免循环填像素代码刷屏
            }
        }

        private static bool HasParameterTypeIn(MethodDeclarationSyntax method, HashSet<string> typeNames, out string? matchedParamName)
        {
            matchedParamName = null;
            foreach (var p in method.ParameterList.Parameters)
            {
                if (p.Type == null)
                    continue;
                string simple = ExtractSimpleTypeName(p.Type);
                if (typeNames.Contains(simple))
                {
                    matchedParamName = p.Identifier.ValueText;
                    return true;
                }
            }
            return false;
        }

        private static bool IsLikelyDrawMethod(MethodDeclarationSyntax method)
        {
            string name = method.Identifier.ValueText;
            if (name.Equals("draw", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Draw", StringComparison.Ordinal))
            {
                return method.ParameterList.Parameters.Any(p =>
                    p.Type != null && ExtractSimpleTypeName(p.Type) == "SpriteBatch");
            }
            return false;
        }

        private static bool IsInsideNestedLambdaOrLocalFunction(SyntaxNode node, SyntaxNode stopAt)
        {
            for (SyntaxNode? cur = node.Parent; cur != null && cur != stopAt; cur = cur.Parent)
            {
                if (cur is LambdaExpressionSyntax || cur is AnonymousMethodExpressionSyntax || cur is LocalFunctionStatementSyntax)
                    return true;
            }
            return false;
        }

        private static string? TryGetSecondLambdaParamName(LambdaExpressionSyntax lambda)
        {
            if (lambda is ParenthesizedLambdaExpressionSyntax pl && pl.ParameterList.Parameters.Count >= 2)
                return pl.ParameterList.Parameters[1].Identifier.ValueText;
            return null;
        }

        private static string? TryExtractLastMemberName(ExpressionSyntax expr)
        {
            if (expr is IdentifierNameSyntax id)
                return id.Identifier.ValueText;
            if (expr is MemberAccessExpressionSyntax ma)
                return ma.Name.Identifier.ValueText;
            return null;
        }

        private static string ExtractSimpleTypeName(TypeSyntax type)
        {
            if (type is IdentifierNameSyntax id)
                return id.Identifier.ValueText;
            if (type is QualifiedNameSyntax qn)
                return qn.Right.Identifier.ValueText;
            if (type is GenericNameSyntax gn)
                return gn.Identifier.ValueText;
            string raw = type.ToString();
            int dot = raw.LastIndexOf('.');
            return dot >= 0 ? raw.Substring(dot + 1) : raw;
        }

        private static int GetLineNumber(SyntaxNode node)
        {
            return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        }
    }
}
