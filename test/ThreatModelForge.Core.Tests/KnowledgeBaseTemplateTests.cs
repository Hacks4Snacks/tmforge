namespace ThreatModelForge.Core.Tests
{
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;

    /// <summary>
    /// Tests the observable Microsoft Threat Modeling Tool template contract.
    /// </summary>
    [TestClass]
    public class KnowledgeBaseTemplateTests
    {
        /// <summary>
        /// Verifies that official TB7 names and modeled semantic fields survive a round trip.
        /// </summary>
        [TestMethod]
        public void OfficialFieldsRoundTripSemantically()
        {
            const string source = """
                <?xml version="1.0" encoding="utf-8"?>
                <KnowledgeBase>
                  <Manifest name="Fidelity fixture" id="11111111-1111-1111-1111-111111111111" version="1.0" author="Microsoft" />
                  <ThreatMetaData><IsPriorityUsed>true</IsPriorityUsed><IsStatusUsed>false</IsStatusUsed><PropertiesMetaData>
                    <ThreatMetaDatum><Name>Priority</Name><Label>Priority</Label><Description>Priority description</Description><HideFromUI>false</HideFromUI><Values><Value>High</Value></Values><Id>priority-id</Id><AttributeType>1</AttributeType></ThreatMetaDatum>
                  </PropertiesMetaData></ThreatMetaData>
                  <GenericElements>
                    <ElementType>
                      <Name>Process</Name><ID>GE.P</ID><Description>Process description</Description><ParentElement>ROOT</ParentElement>
                      <Image>image-data</Image><Hidden>false</Hidden><Behavior>Resizable</Behavior><Shape>Ellipse</Shape>
                      <Representation>Ellipse</Representation><StrokeThickness>2.5</StrokeThickness><StrokeDashArray>4 2</StrokeDashArray>
                      <ImageLocation>process.png</ImageLocation>
                      <Attributes>
                        <Attribute><Id>attribute-id</Id><IsInherited>true</IsInherited><IsOverrided>false</IsOverrided>
                          <Name>internal-name</Name><DisplayName>Authentication</DisplayName><Mode>Static</Mode><Type>List</Type>
                          <Inheritance>New</Inheritance><AttributeValues><Value>Yes</Value><Value>No</Value></AttributeValues>
                        </Attribute>
                      </Attributes>
                    </ElementType>
                  </GenericElements>
                  <StandardElements />
                  <ThreatCategories><ThreatCategory><Name>Privacy</Name><Id>privacy</Id><ShortDescription>Privacy</ShortDescription><LongDescription>Privacy risk</LongDescription></ThreatCategory></ThreatCategories>
                  <ThreatTypes>
                    <ThreatType>
                      <GenerationFilters><Include>target is 'GE.P'</Include><Exclude>target.Authentication is 'Yes'</Exclude></GenerationFilters>
                      <Id>threat-id</Id><ShortTitle>Inspect {target.Name}</ShortTitle><Category>privacy</Category>
                      <RelatedCategory>related-category</RelatedCategory><Description>Threat description</Description><PropertiesMetaData>
                        <ThreatMetaDatum><Name>Priority</Name><Label>Priority</Label><Description>Threat priority</Description><HideFromUI>false</HideFromUI><Values><Value>High</Value></Values><Id>threat-priority-id</Id><AttributeType>1</AttributeType></ThreatMetaDatum>
                      </PropertiesMetaData>
                    </ThreatType>
                  </ThreatTypes>
                </KnowledgeBase>
                """;

            KnowledgeBaseData original = Load(source);
            AssertSemantics(original);

            using MemoryStream saved = new MemoryStream();
            original.Save(saved);
            string output = Encoding.UTF8.GetString(saved.ToArray());
            XDocument document = XDocument.Parse(output);

            Assert.AreEqual(1, document.Descendants("Mode").Count());
            Assert.AreEqual(1, document.Descendants("Inheritance").Count());
            Assert.AreEqual(0, document.Descendants("AttributeMode").Count());
            Assert.AreEqual(0, document.Descendants("AttributeInheritance").Count());

            saved.Position = 0;
            KnowledgeBaseData reloaded = KnowledgeBaseData.Load(saved);
            AssertSemantics(reloaded);
        }

        /// <summary>
        /// Verifies compatibility with templates previously emitted using legacy alias names.
        /// </summary>
        [TestMethod]
        public void LegacyAttributeAliasesRemainReadable()
        {
            const string source = """
                <KnowledgeBase>
                  <GenericElements><ElementType><Attributes><Attribute>
                    <AttributeMode>Static</AttributeMode><AttributeInheritance>New</AttributeInheritance>
                  </Attribute></Attributes></ElementType></GenericElements>
                </KnowledgeBase>
                """;

            KnowledgeBaseData knowledgeBase = Load(source);
            KnowledgeBaseAttribute attribute = knowledgeBase.GenericElements.Single().Attributes.Single();

            Assert.AreEqual(AttributeMode.Static, attribute.Mode);
            Assert.AreEqual(AttributeInheritance.New, attribute.Inheritance);
        }

        private static KnowledgeBaseData Load(string source)
        {
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
            return KnowledgeBaseData.Load(stream);
        }

        private static void AssertSemantics(KnowledgeBaseData knowledgeBase)
        {
            Assert.AreEqual("Priority description", knowledgeBase.ThreatMetaData!.PropertiesMetaData.Single().Description);

            ElementType element = knowledgeBase.GenericElements.Single();
            Assert.AreEqual("Resizable", element.Behavior);
            Assert.AreEqual("Ellipse", element.Shape);
            Assert.AreEqual("4 2", element.StrokeDashArray);

            KnowledgeBaseAttribute attribute = element.Attributes.Single();
            Assert.AreEqual(AttributeMode.Static, attribute.Mode);
            Assert.AreEqual(AttributeInheritance.New, attribute.Inheritance);

            ThreatType threat = knowledgeBase.ThreatTypes.Single();
            Assert.AreEqual("related-category", threat.RelatedCategory);
            Assert.AreEqual("target is 'GE.P'", threat.GenerationFilters.Include);
            Assert.AreEqual("target.Authentication is 'Yes'", threat.GenerationFilters.Exclude);
            Assert.AreEqual("Threat priority", threat.PropertiesMetaData.Single().Description);
        }
    }
}
