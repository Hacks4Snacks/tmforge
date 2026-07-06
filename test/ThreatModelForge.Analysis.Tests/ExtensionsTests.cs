namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="Extensions"/> class.
    /// </summary>
    [TestClass]
    public class ExtensionsTests
    {
        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Unit test for the <see cref="Extensions.TrustBoundaryBorders(DrawingSurfaceModel)"/> method.
        /// </summary>
        [TestMethod]
        public void TrustBoundaryBordersTest()
        {
            BorderBoundary b1 = new BorderBoundary
            {
                Guid = Guid.NewGuid(),
            };

            BorderBoundary b2 = new BorderBoundary
            {
                Guid = Guid.NewGuid(),
            };

            BorderBoundary b3 = new BorderBoundary
            {
                Guid = Guid.NewGuid(),
            };

            ThreatModel model = new ThreatModel
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { b1.Guid, b1 },
                            { b2.Guid, b2 },
                        },
                    },
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-1",
                        Borders =
                        {
                            { b3.Guid, b3 },
                        },
                    },
                },
            };

            IEnumerable<BorderBoundary> actual = model.DrawingSurfaceList[0].TrustBoundaryBorders();
            Assert.IsNotNull(actual);
            Assert.AreEqual(2, actual.Count());
        }

        /// <summary>
        /// Unit test for the <see cref="Extensions.TrustBoundaryLines(DrawingSurfaceModel)"/> method.
        /// </summary>
        [TestMethod]
        public void TrustBoundaryLinesTest()
        {
            LineBoundary l1 = new LineBoundary
            {
                Guid = Guid.NewGuid(),
            };

            DrawingSurfaceModel model = new DrawingSurfaceModel
            {
                Header = "DFD-0",
                Lines =
                {
                    { l1.Guid, l1 },
                },
            };

            IEnumerable<LineBoundary> actual = model.TrustBoundaryLines();
            Assert.IsNotNull(actual);
            Assert.AreEqual(1, actual.Count());
        }

        /// <summary>
        /// Unit test for the <see cref="Extensions.Crosses(Connector, BorderBoundary)"/> method.
        /// </summary>
        [TestMethod]
        public void CrossesBorderBoundaryTest()
        {
            BorderBoundary border = new BorderBoundary
            {
                Guid = Guid.NewGuid(),
                Left = 5,
                Top = 10,
                Height = 15,
                Width = 20,
            };

            Connector inside = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 10,
                SourceY = 11,
                TargetX = 12,
                TargetY = 15,
            };

            Connector crossesIn = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 10,
                TargetY = 11,
            };

            Connector crossesOut = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 11,
                SourceY = 12,
                TargetX = 100,
                TargetY = 100,
            };

            Connector outside = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 50,
                SourceY = 40,
                TargetX = 100,
                TargetY = 100,
            };

            Assert.IsFalse(inside.Crosses(border));
            Assert.IsTrue(crossesIn.Crosses(border));
            Assert.IsTrue(crossesOut.Crosses(border));
            Assert.IsFalse(outside.Crosses(border));
        }

        /// <summary>
        /// Unit test for the <see cref="Extensions.Crosses(Connector, LineBoundary)"/> method.
        /// </summary>
        [TestMethod]
        public void CrossesLineBoundaryTest()
        {
            Tuple<Connector, LineBoundary, bool>[] testCases = new[]
            {
                new Tuple<Connector, LineBoundary, bool>(
                    new Connector { SourceX = 0, SourceY = 0, TargetX = 10, TargetY = 10 },
                    new LineBoundary { SourceX = 10, SourceY = 0, TargetX = 0, TargetY = 10 },
                    true),
                new Tuple<Connector, LineBoundary, bool>(
                    new Connector { SourceX = 10, SourceY = 0, TargetX = 0, TargetY = 10 },
                    new LineBoundary { SourceX = 0, SourceY = 0, TargetX = 10, TargetY = 10 },
                    true),
                new Tuple<Connector, LineBoundary, bool>(
                    new Connector { SourceX = 1, SourceY = 2, TargetX = 11, TargetY = 2 },
                    new LineBoundary { SourceX = 0, SourceY = 4, TargetX = 9, TargetY = 4 },
                    false),
                new Tuple<Connector, LineBoundary, bool>(
                    new Connector { SourceX = 1, SourceY = 2, TargetX = 11, TargetY = 2 },
                    new LineBoundary { SourceX = 1, SourceY = 3, TargetX = 1, TargetY = 4 },
                    false),
                new Tuple<Connector, LineBoundary, bool>(
                    new Connector { SourceX = 1, SourceY = 2, TargetX = 11, TargetY = 2 },
                    new LineBoundary { SourceX = 1, SourceY = 3, TargetX = 1, TargetY = 4 },
                    false),
                new Tuple<Connector, LineBoundary, bool>(
                    new Connector { SourceX = 1, SourceY = 2, TargetX = 11, TargetY = 2 },
                    new LineBoundary { SourceX = 1, SourceY = 2, TargetX = 1, TargetY = 4 },
                    true),
                new Tuple<Connector, LineBoundary, bool>(
                    new Connector { SourceX = 1, SourceY = 2, TargetX = 11, TargetY = 2 },
                    new LineBoundary { SourceX = 5, SourceY = 2, TargetX = 1, TargetY = 4 },
                    true),
            };

            for (int i = 0; i < testCases.Length; i++)
            {
                var testCase = testCases[i];
                Assert.AreEqual(
                    testCase.Item3,
                    testCase.Item1.Crosses(testCase.Item2),
                    i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Unit test for the <see cref="Extensions.TrustBoundaryCrossings(DrawingSurfaceModel, Connector)"/> method.
        /// </summary>
        [TestMethod]
        public void TrustBoundaryCrossingsTest()
        {
            BorderBoundary border = new BorderBoundary
            {
                Guid = Guid.NewGuid(),
                Left = 5,
                Top = 10,
                Height = 15,
                Width = 20,
            };

            Connector inside = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 10,
                SourceY = 11,
                TargetX = 12,
                TargetY = 15,
            };

            Connector crossesIn = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 10,
                TargetY = 11,
            };

            Connector crossesOut = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 11,
                SourceY = 12,
                TargetX = 100,
                TargetY = 100,
            };

            Connector outside = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 50,
                SourceY = 40,
                TargetX = 100,
                TargetY = 100,
            };

            LineBoundary line = new LineBoundary
            {
                Guid = Guid.NewGuid(),
                SourceX = 100,
                SourceY = 90,
                TargetX = 90,
                TargetY = 100,
            };

            DrawingSurfaceModel model = new DrawingSurfaceModel()
            {
                Header = "DFD-0",
                Borders =
                {
                    { border.Guid, border },
                },
                Lines =
                {
                    { inside.Guid, inside },
                    { crossesIn.Guid, crossesIn },
                    { crossesOut.Guid, crossesOut },
                    { outside.Guid, outside },
                    { line.Guid, line },
                },
            };

            IEnumerable<Entity> actual = model.TrustBoundaryCrossings(inside);
            Assert.AreEqual(0, actual.Count());
            actual = model.TrustBoundaryCrossings(crossesIn);
            Assert.AreEqual(1, actual.Count());
            Assert.AreEqual(border, actual.First());
            actual = model.TrustBoundaryCrossings(crossesOut);
            Assert.AreEqual(2, actual.Count());
            Assert.AreEqual(border, actual.First());
            Assert.AreEqual(line, actual.Skip(1).First());
            actual = model.TrustBoundaryCrossings(outside);
            Assert.AreEqual(1, actual.Count());
            Assert.AreEqual(line, actual.First());
        }

        /// <summary>
        /// Unit test for the <see cref="Extensions.ExternalInteractors(DrawingSurfaceModel)"/> method.
        /// </summary>
        [TestMethod]
        public void ExternalInteractorsTest()
        {
            StencilRectangle b1 = new StencilRectangle
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = "GE.EI",
            };

            StencilEllipse b2 = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = "GE.EI",
            };

            DrawingSurfaceModel target = new DrawingSurfaceModel
            {
                Borders =
                {
                    { b1.Guid, b1 },
                    { b2.Guid, b2 },
                },
            };

            IEnumerable<Entity> actual = target.ExternalInteractors();
            Assert.IsNotNull(actual);
            Assert.AreEqual(2, actual.Count());
        }

        /// <summary>
        /// Unit test for the <see cref="Extensions.IsGenericComponent(Entity)"/> method.
        /// </summary>
        [TestMethod]
        public void IsGenericComponentTest()
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };

            void Add(Entity entity, string header)
            {
                entity.Guid = Guid.NewGuid();
                entity.Properties.Add(new HeaderDisplayAttribute { Name = header, DisplayName = header });
                diagram.Borders.Add(entity.Guid, entity);
            }

            // Generic components: the generic type id matches the type id.
            Add(new StencilEllipse { GenericTypeId = "GE.P", TypeId = "GE.P" }, "Generic Process");
            Add(new StencilRectangle { GenericTypeId = "GE.EI", TypeId = "GE.EI" }, "Generic External Interactor");
            Add(new StencilParallelLines { GenericTypeId = "GE.DS", TypeId = "GE.DS" }, "Generic Data Store");

            // Specific components: the type id is more specific than the generic type id.
            Add(new StencilEllipse { GenericTypeId = "GE.P", TypeId = "SE.P.TMCore.OSProcess" }, "OS Process");
            Add(new StencilRectangle { GenericTypeId = "GE.EI", TypeId = "SE.EI.TMCore.Browser" }, "Browser");
            Add(new StencilParallelLines { GenericTypeId = "GE.DS", TypeId = "SE.DS.TMCore.CloudStorage" }, "Cloud Storage");

            // A trust boundary box is not a component and therefore not a generic component.
            Add(new BorderBoundary { GenericTypeId = "GE.TB.B", TypeId = "GE.TB.B" }, "Generic Trust Border Boundary");

            ThreatModel model = new ThreatModel { DrawingSurfaceList = { diagram } };

            var diag = model.DrawingSurfaceList.FirstOrDefault();
            Assert.IsNotNull(diag);

            var ents = diag!
                .Borders
                .Values
                .OfType<Entity>();

            // Look at the generic components in the model.
            Entity? genericProcess = ents.FirstOrDefault(e => string.Equals(e.HeaderName(), "Generic Process"));
            Assert.IsNotNull(genericProcess);
            Assert.IsTrue(genericProcess!.IsGenericComponent());
            Entity? genericExtInt = ents.FirstOrDefault(e => string.Equals(e.HeaderName(), "Generic External Interactor"));
            Assert.IsNotNull(genericExtInt);
            Assert.IsTrue(genericExtInt!.IsGenericComponent());
            Entity? genericStore = ents.FirstOrDefault(e => string.Equals(e.HeaderName(), "Generic Data Store"));
            Assert.IsNotNull(genericStore);
            Assert.IsTrue(genericStore!.IsGenericComponent());

            // Look at the non-generic components in the model.
            Entity? osProcess = ents.FirstOrDefault(e => string.Equals(e.HeaderName(), "OS Process"));
            Assert.IsNotNull(osProcess);
            Assert.IsFalse(osProcess!.IsGenericComponent());
            Entity? browser = ents.FirstOrDefault(e => string.Equals(e.HeaderName(), "Browser"));
            Assert.IsNotNull(browser);
            Assert.IsFalse(browser!.IsGenericComponent());
            Entity? cloudStorage = ents.FirstOrDefault(e => string.Equals(e.HeaderName(), "Cloud Storage"));
            Assert.IsNotNull(cloudStorage);
            Assert.IsFalse(cloudStorage!.IsGenericComponent());

            // Look at a generic trust boundary, which is not a component and not a generic component.
            Entity? trustBound = ents.FirstOrDefault(e => string.Equals(e.HeaderName(), "Generic Trust Border Boundary"));
            Assert.IsNotNull(trustBound);
            Assert.IsFalse(trustBound!.IsGenericComponent());
        }
    }
}
