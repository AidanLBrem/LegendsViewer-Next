using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;

namespace LegendsViewer.Backend.Tests.Legends.Parser;

[TestClass]
public class ConspiracyTests
{
    private static Property P(string name, string value) => new() { Name = name, Value = value };

    [TestMethod]
    public void MissingIdsDoNotResolveToFigureOrEntityZero()
    {
        var world = new World();
        world.HistoricalFigures.Add(new HistoricalFigure { Id = 0, Name = "Unrelated zero" });
        var owner = new HistoricalFigure { Id = 42, Name = "Owner", IntriguePlots = [new IntriguePlot([P("local_id", "0")])] };
        world.HistoricalFigures.Add(owner);
        var plot = ConspiracyAnalysis.Calculate(world).Single();
        Assert.IsNull(plot.Entity);
        Assert.IsNull(plot.Artifact);
        Assert.IsNull(plot.Parent);
        Assert.AreEqual(0, plot.Participants.Count);
        Assert.AreEqual("Unknown objective", plot.Objective);
        Assert.AreEqual("Not marked on hold", plot.Status);
    }

    [TestMethod]
    public void ActorsAndPlotsUseOwnerScopedIdsAndTargetsRemainDistinct()
    {
        var world = new World();
        foreach (var id in new[] { 10, 20 })
            world.HistoricalFigures.Add(new HistoricalFigure { Id = id, Name = $"Owner {id}",
                IntrigueActors = [new IntrigueActor([P("local_id", "0"), P("hfid", (id + 1).ToString()), P("role", "enemy"), P("strategy", "neutralize")])],
                IntriguePlots = [new IntriguePlot([P("local_id", "0"), P("type", "assassinate actor"), P("actor_id", "0")])] });
        world.HistoricalFigures.Add(new HistoricalFigure { Id = 11, Name = "Target" });
        var plots = ConspiracyAnalysis.Calculate(world);
        Assert.AreEqual("10:0", plots[0].Key);
        Assert.AreEqual("20:0", plots[1].Key);
        Assert.AreEqual("/hf/11", plots[0].Participants.Single().Figure?.Path);
        Assert.IsTrue(plots[0].Participants.Single().IsTarget);
        Assert.AreEqual("enemy", plots[0].Participants.Single().ActorRole);
        Assert.AreEqual("neutralize", plots[0].Participants.Single().Strategy);
        Assert.AreEqual(21, plots[1].Participants.Single().Figure?.Id);
        Assert.IsNull(plots[1].Participants.Single().Figure?.Path);
    }

    [TestMethod]
    public void FlagsRespectFalseAndRawFieldsPreserveNestedUnknownObjectives()
    {
        Assert.IsTrue(new IntriguePlot([P("on_hold", "")]).OnHold);
        Assert.IsFalse(new IntriguePlot([P("on_hold", "false")]).OnHold);
        Assert.IsFalse(new IntrigueActor([P("promised_me_immortality", "0")]).PromisedMeImmortality);
        var plot = new IntriguePlot([new Property { Name = "future_objective", SubProperties = [P("target", "99")] }]);
        Assert.AreEqual("99", plot.Fields.Single().Children.Single().Value);
    }

    [TestMethod]
    public async Task XmlPairMergesByLocalIdWithoutLosingDistinctPlotsOrActors()
    {
        const string legends = """
            <df_world><historical_figures><historical_figure><id>10</id><name>owner</name><race>DWARF</race>
            <intrigue_actor><local_id>0</local_id><hfid>11</hfid><role>asset</role></intrigue_actor>
            <intrigue_plot><local_id>0</local_id><type>acquire artifact</type><artifact_id>12</artifact_id>
              <plot_actor><actor_id>0</actor_id><plot_role>thief</plot_role></plot_actor></intrigue_plot>
            <intrigue_plot><local_id>1</local_id><type>undead world conquest</type></intrigue_plot>
            </historical_figure></historical_figures></df_world>
            """;
        const string plus = """
            <df_world><historical_figures><historical_figure><id>10</id>
            <intrigue_actor><local_id>0</local_id><strategy>use</strategy></intrigue_actor>
            <intrigue_actor><local_id>1</local_id><hfid>22</hfid></intrigue_actor>
            <intrigue_plot><local_id>0</local_id><on_hold/>
              <plot_actor><actor_id>0</actor_id><agreement_id>7</agreement_id></plot_actor>
              <plot_actor><actor_id>1</actor_id><plot_role>source of funds</plot_role></plot_actor></intrigue_plot>
            <intrigue_plot><local_id>2</local_id><type>infiltrate society</type></intrigue_plot>
            </historical_figure></historical_figures></df_world>
            """;
        var first = Path.GetTempFileName(); var second = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(first, legends); await File.WriteAllTextAsync(second, plus);
            var world = new World();
            using var parser = new XmlParser(world, first, second);
            await parser.ParseAsync();
            var owner = world.HistoricalFigures.Single();
            Assert.AreEqual(3, owner.IntriguePlots.Count);
            Assert.AreEqual(2, owner.IntrigueActors.Count);
            var plot = owner.IntriguePlots.Single(p => p.LocalId == 0);
            Assert.AreEqual("acquire artifact", plot.Type);
            Assert.IsTrue(plot.OnHold);
            Assert.AreEqual(2, plot.PlotActors.Count);
            Assert.AreEqual("thief", plot.PlotActors[0].PlotRole);
            Assert.AreEqual(7, plot.PlotActors[0].AgreementId);
            Assert.AreEqual(11, owner.IntrigueActors[0].HfId);
            Assert.AreEqual("use", owner.IntrigueActors[0].Strategy);
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [TestMethod]
    public void ApiFiltersPaginatesAndRejectsInvalidRequests()
    {
        var world = new World();
        var civ = new Entity([], world) { Id = 7, Name = "Civilization", IsCiv = true };
        world.Entities.Add(civ);
        world.HistoricalFigures.Add(new HistoricalFigure { Id = 10, Name = "Owner", IntriguePlots = [
            new IntriguePlot([P("local_id", "0"), P("entity_id", "7"), P("type", "infiltrate society"), P("on_hold", "")]),
            new IntriguePlot([P("local_id", "1"), P("type", "acquire artifact")])] });
        var controller = new ConspiracyController(world);
        var response = (ConspiracyPageDto)((OkObjectResult)controller.Get(7, "INFILTRATE", "on-hold").Result!).Value!;
        Assert.AreEqual(2, response.TotalCount);
        Assert.AreEqual(1, response.TotalFilteredCount);
        Assert.AreEqual("10:0", response.Items.Single().Key);
        Assert.AreEqual(7, response.Civilizations.Single().Id);
        Assert.IsInstanceOfType(controller.Get(page: 0).Result, typeof(BadRequestObjectResult));
        Assert.IsInstanceOfType(controller.Get(status: "successful").Result, typeof(BadRequestObjectResult));
        Assert.IsInstanceOfType(controller.GetPlot(999, 0).Result, typeof(NotFoundResult));
        var second = (ConspiracyPageDto)((OkObjectResult)controller.Get(page: 2, pageSize: 1).Result!).Value!;
        Assert.AreEqual("10:1", second.Items.Single().Key);
    }

    [TestMethod]
    public async Task OptionalRealExportSmokeTest()
    {
        var standard = Environment.GetEnvironmentVariable("LEGENDS_SMOKE_XML");
        if (string.IsNullOrEmpty(standard)) { Assert.Inconclusive("Set LEGENDS_SMOKE_XML and optionally LEGENDS_SMOKE_PLUS to test local exports."); return; }
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var world = new World();
        using var parser = new XmlParser(world, standard, Environment.GetEnvironmentVariable("LEGENDS_SMOKE_PLUS"));
        await parser.ParseAsync();
        var plots = ConspiracyAnalysis.Calculate(world);
        Assert.AreEqual(world.HistoricalFigures.Sum(f => f.IntriguePlots.Count), plots.Count);
        Assert.IsTrue(plots.Count > 0);
        Assert.AreEqual(plots.Count, plots.Select(p => p.Key).Distinct().Count());
        Console.WriteLine($"Loaded {world.HistoricalFigures.Count} figures, {world.Entities.Count} entities, {plots.Count} plots, {world.HistoricalFigures.Sum(f => f.IntrigueActors.Count)} actors.");
        Console.WriteLine($"On hold: {plots.Count(p => p.Status == "On hold")}; unresolved figures: {plots.SelectMany(p => p.Participants).Count(a => a.Figure != null && a.Figure.Path == null)}.");
    }
}
