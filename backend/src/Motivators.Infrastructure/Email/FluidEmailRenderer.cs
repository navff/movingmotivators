using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Fluid;
using Motivators.Application.Email;

namespace Motivators.Infrastructure.Email;

/// <summary>
/// Шаблоны Liquid (embedded resources из Email/Templates): {name}.html.liquid оборачивается в _layout.html.liquid,
/// {name}.txt.liquid — plain text.
/// </summary>
public class FluidEmailRenderer : IEmailRenderer
{
    private static readonly FluidParser Parser = new();
    // без экранирования кириллицы в &#x...; — письма должны читаться в исходнике и в превью
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);
    private readonly ConcurrentDictionary<string, IFluidTemplate> _cache = new();
    private readonly TemplateOptions _options = new() { MemberAccessStrategy = new UnsafeMemberAccessStrategy() };

    public (string Html, string Text) Render(string template, object model)
    {
        var body = RenderTemplate($"{template}.html.liquid", model, Html);
        var html = RenderTemplate("_layout.html.liquid", model, Html, ("content", body));
        var text = RenderTemplate($"{template}.txt.liquid", model, NullEncoder.Default);
        return (html, text);
    }

    private string RenderTemplate(string name, object model, TextEncoder encoder, params (string Key, object Value)[] extra)
    {
        var tpl = _cache.GetOrAdd(name, Load);
        var ctx = new TemplateContext(_options);
        foreach (var (k, v) in ToPairs(model)) ctx.SetValue(k, v);
        foreach (var (k, v) in extra) ctx.SetValue(k, v);
        return tpl.Render(ctx, encoder);
    }

    private static IEnumerable<(string, object?)> ToPairs(object model)
    {
        if (model is IDictionary dict)
        {
            foreach (DictionaryEntry e in dict) yield return (e.Key.ToString()!, e.Value);
            yield break;
        }
        foreach (var p in model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            yield return (p.Name, p.GetValue(model));
    }

    private static IFluidTemplate Load(string name)
    {
        var asm = typeof(FluidEmailRenderer).Assembly;
        var resource = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(".Templates." + name))
                       ?? throw new InvalidOperationException($"Шаблон письма {name} не найден");
        using var stream = asm.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();
        if (!Parser.TryParse(source, out var tpl, out var error))
            throw new InvalidOperationException($"Ошибка в шаблоне {name}: {error}");
        return tpl;
    }
}
