namespace ThreatModelForge.Engine
{
    using System.Globalization;
    using System.Text;
    using System.Text.Json;
    using System.Xml;
    using System.Xml.Linq;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Formats;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>Applies canvas edits to a retained native document without rebuilding its unedited data.</summary>
    internal static class NativeTm7Document
    {
        private static readonly XNamespace Instance = "http://www.w3.org/2001/XMLSchema-instance";

        /// <summary>Saves supported edits while retaining unrepresented native XML.</summary>
        /// <param name="original">The native source bytes.</param>
        /// <param name="edited">The edited canvas projection.</param>
        /// <returns>The original bytes for a no-op, otherwise the patched native document.</returns>
        internal static byte[] Save(byte[] original, TmForgeModelDto edited)
        {
            if (original.Length > JsonDocumentPreflight.MaxBytes)
            {
                throw new InvalidDataException("Native TM7 editing is limited to 8 MiB documents.");
            }

            XDocument retained = ReadXml(original);
            PreflightResultDto preflight = DocumentPreflight.Inspect(original, Tm7Format.FormatId);
            if (!preflight.Success)
            {
                throw new InvalidDataException(string.Join(" ", preflight.Diagnostics.Select(item => item.Message)));
            }

            using MemoryStream input = new MemoryStream(original, writable: false);
            ThreatModel native = ThreatModel.Load(input);
            TmForgeModelDto baseline = ModelDtoMapper.ToDto(native);
            ThreatModel beforeProjection = ModelDtoMapper.ToModel(baseline);
            ThreatModel requested = ModelDtoMapper.ToModel(edited);
            XDocument before = Serialize(native);
            ApplyPages(native, beforeProjection, requested);
            if (!Same(baseline.Metadata, edited.Metadata))
            {
                native.MetaInformation = edited.Metadata;
            }

            ApplyThreats(native, baseline.Threats, edited.Threats, requested);

            XDocument after = Serialize(native);
            if (XNode.DeepEquals(before, after))
            {
                return original.ToArray();
            }

            Patch(retained.Root!, before.Root!, after.Root!);
            using MemoryStream output = new MemoryStream();
            using (XmlWriter writer = XmlWriter.Create(output, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = retained.Declaration == null,
                NewLineHandling = NewLineHandling.None,
                CloseOutput = false,
            }))
            {
                retained.Save(writer);
            }

            byte[] saved = output.ToArray();
            PreflightResultDto checkedOutput = DocumentPreflight.Inspect(saved, Tm7Format.FormatId);
            if (!checkedOutput.Success)
            {
                throw new InvalidDataException("The native edit could not be saved without invalidating the document.");
            }

            return saved;
        }

        private static void ApplyPages(ThreatModel native, ThreatModel baseline, ThreatModel requested)
        {
            Dictionary<Guid, DrawingSurfaceModel> oldPages = baseline.DrawingSurfaceList.ToDictionary(page => page.Guid);
            Dictionary<Guid, DrawingSurfaceModel> newPages = requested.DrawingSurfaceList.ToDictionary(page => page.Guid);
            Dictionary<Guid, DrawingSurfaceModel> nativePages = native.DrawingSurfaceList.ToDictionary(page => page.Guid);
            Dictionary<Guid, Guid> oldOwners = baseline.DrawingSurfaceList.SelectMany(page => page.Borders.Keys.Concat(page.Lines.Keys)
                .Select(id => (Id: id, Page: page.Guid))).ToDictionary(item => item.Id, item => item.Page);
            foreach (DrawingSurfaceModel page in requested.DrawingSurfaceList)
            {
                if (page.Borders.Keys.Concat(page.Lines.Keys).Any(id => oldOwners.TryGetValue(id, out Guid owner) && owner != page.Guid))
                {
                    throw new NotSupportedException("Moving an existing native object between pages is not supported; its unrepresented data must stay with its original page.");
                }
            }

            foreach (DrawingSurfaceModel page in native.DrawingSurfaceList.ToArray())
            {
                DrawingSurfaceModel before = oldPages[page.Guid];
                if (!newPages.TryGetValue(page.Guid, out DrawingSurfaceModel? after))
                {
                    if (page.Borders.Count != before.Borders.Count || page.Lines.Count != before.Lines.Count)
                    {
                        throw new NotSupportedException("The page contains native objects that are not editable in Studio. Delete it in MTMT to avoid losing hidden data.");
                    }

                    EnsureUnreferenced(native, page.Guid);
                    foreach (Guid id in page.Borders.Keys.Concat(page.Lines.Keys))
                    {
                        EnsureUnreferenced(native, id);
                    }

                    native.DrawingSurfaceList.Remove(page);
                    continue;
                }

                if (before.Header != after.Header)
                {
                    page.Header = after.Header;
                    DiagramElementHelper.SetName(page, after.Header ?? string.Empty);
                }

                ApplyObjects(native, page.Borders, before.Borders, after.Borders);
                ApplyObjects(native, page.Lines, before.Lines, after.Lines);
                UpdateConnectors(page, before, after);
            }

            foreach (DrawingSurfaceModel page in requested.DrawingSurfaceList.Where(page => !oldPages.ContainsKey(page.Guid)))
            {
                foreach (Entity element in page.Borders.Values.Concat(page.Lines.Values).OfType<Entity>())
                {
                    ValidateAddition(native, element);
                }

                nativePages.Add(page.Guid, page);
            }

            native.DrawingSurfaceList.Clear();
            foreach (DrawingSurfaceModel page in requested.DrawingSurfaceList)
            {
                native.DrawingSurfaceList.Add(nativePages[page.Guid]);
            }
        }

        private static void ApplyObjects(ThreatModel model, IDictionary<Guid, object> native, IDictionary<Guid, object> baseline, IDictionary<Guid, object> requested)
        {
            foreach (Guid id in baseline.Keys.Where(id => !requested.ContainsKey(id)))
            {
                EnsureUnreferenced(model, id);
                native.Remove(id);
            }

            foreach (KeyValuePair<Guid, object> pair in requested.Where(pair => !baseline.ContainsKey(pair.Key)))
            {
                if (native.ContainsKey(pair.Key))
                {
                    throw new NotSupportedException("A new object collides with an unrepresented native object identity.");
                }

                ValidateAddition(model, (Entity)pair.Value);
                native.Add(pair.Key, pair.Value);
            }

            foreach (KeyValuePair<Guid, object> pair in baseline.Where(pair => requested.ContainsKey(pair.Key)))
            {
                Entity before = (Entity)pair.Value;
                Entity after = (Entity)requested[pair.Key];
                Entity target = (Entity)native[pair.Key];
                if (before.GetType() != after.GetType())
                {
                    throw new NotSupportedException("Changing an existing native stencil kind is not supported.");
                }

                if (DiagramElementHelper.GetName(before) != DiagramElementHelper.GetName(after))
                {
                    DiagramElementHelper.SetName(target, DiagramElementHelper.GetName(after));
                }

                ApplyProperties(target, DiagramElementHelper.GetCustomProperties(before), DiagramElementHelper.GetCustomProperties(after));
                if (before is DrawingElement oldBox && after is DrawingElement newBox
                    && (oldBox.Left != newBox.Left || oldBox.Top != newBox.Top || oldBox.Width != newBox.Width || oldBox.Height != newBox.Height))
                {
                    ValidateBox(newBox);
                    DrawingElement box = (DrawingElement)target;
                    box.Left = newBox.Left;
                    box.Top = newBox.Top;
                    box.Width = newBox.Width;
                    box.Height = newBox.Height;
                }
            }

            Queue<KeyValuePair<Guid, object>> ordered = new Queue<KeyValuePair<Guid, object>>(
                requested.Keys.Select(id => new KeyValuePair<Guid, object>(id, native[id])));
            KeyValuePair<Guid, object>[] entries = native.Select(pair => requested.ContainsKey(pair.Key) ? ordered.Dequeue() : pair).ToArray();
            native.Clear();
            foreach (KeyValuePair<Guid, object> entry in entries)
            {
                native.Add(entry.Key, entry.Value);
            }
        }

        private static void EnsureUnreferenced(ThreatModel model, Guid id)
        {
            if (model.AllThreatsDictionary.Values.Any(threat => threat.DrawingSurfaceGuid == id
                || threat.SourceGuid == id || threat.TargetGuid == id || threat.FlowGuid == id))
            {
                throw new NotSupportedException("Cannot remove object " + id + " because the native threat register references it. Resolve those threats in MTMT before deleting it.");
            }
        }

        private static void ValidateAddition(ThreatModel model, Entity element)
        {
            if (model.KnowledgeBase == null || !model.KnowledgeBase.GenericElements.Concat(model.KnowledgeBase.StandardElements)
                .Any(type => string.Equals(type.Id, element.TypeId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new NotSupportedException("The embedded template does not declare type '" + element.TypeId + "'. Add it in MTMT or export a separate converted model.");
            }

            if (element is DrawingElement box)
            {
                ValidateBox(box);
            }
            else if (element is Connector flow)
            {
                ValidateConnector(flow);
            }
        }

        private static void ValidateBox(DrawingElement box)
        {
            if (box.Left < 10 || box.Top < 10 || box.Left > 1890 || box.Top > 2090 || box.Width <= 0 || box.Height <= 0)
            {
                throw new NotSupportedException("Native TM7 saving requires edited objects within MTMT's canvas (x 10..1890, y 10..2090). Move the object into that range; existing geometry is never shifted automatically.");
            }
        }

        private static void ValidateConnector(Connector flow)
        {
            if (new[] { flow.SourceX, flow.TargetX, flow.HandleX }.Any(value => value < 10 || value > 1990)
                || new[] { flow.SourceY, flow.TargetY, flow.HandleY }.Any(value => value < 10 || value > 2190))
            {
                throw new NotSupportedException("The edited connector exceeds MTMT's canvas. Move its endpoints into range before saving.");
            }
        }

        private static void UpdateConnectors(DrawingSurfaceModel native, DrawingSurfaceModel baseline, DrawingSurfaceModel requested)
        {
            foreach (Connector flow in native.Lines.Values.OfType<Connector>())
            {
                if (!baseline.Lines.TryGetValue(flow.Guid, out object? original))
                {
                    continue;
                }

                Connector before = (Connector)original;
                Connector after = (Connector)requested.Lines[flow.Guid];
                int sourceX = flow.SourceX;
                int sourceY = flow.SourceY;
                int targetX = flow.TargetX;
                int targetY = flow.TargetY;
                int handleX = flow.HandleX;
                int handleY = flow.HandleY;
                if (before.SourceGuid != after.SourceGuid)
                {
                    flow.SourceGuid = after.SourceGuid;
                    flow.SourceX = after.SourceX;
                    flow.SourceY = after.SourceY;
                    flow.PortSource = "None";
                }
                else
                {
                    (flow.SourceX, flow.SourceY) = MoveEndpoint(sourceX, sourceY, (DrawingElement)baseline.Borders[before.SourceGuid], (DrawingElement)requested.Borders[after.SourceGuid]);
                }

                if (before.TargetGuid != after.TargetGuid)
                {
                    flow.TargetGuid = after.TargetGuid;
                    flow.TargetX = after.TargetX;
                    flow.TargetY = after.TargetY;
                    flow.PortTarget = "None";
                }
                else
                {
                    (flow.TargetX, flow.TargetY) = MoveEndpoint(targetX, targetY, (DrawingElement)baseline.Borders[before.TargetGuid], (DrawingElement)requested.Borders[after.TargetGuid]);
                }

                if (sourceX != flow.SourceX || sourceY != flow.SourceY || targetX != flow.TargetX || targetY != flow.TargetY)
                {
                    flow.HandleX = handleX + ((flow.SourceX - sourceX + flow.TargetX - targetX) / 2);
                    flow.HandleY = handleY + ((flow.SourceY - sourceY + flow.TargetY - targetY) / 2);
                    ValidateConnector(flow);
                }
            }
        }

        private static (int X, int Y) MoveEndpoint(int positionX, int positionY, DrawingElement before, DrawingElement after)
        {
            if (before.Left == after.Left && before.Top == after.Top && before.Width == after.Width && before.Height == after.Height)
            {
                return (positionX, positionY);
            }

            return (
                after.Left + (int)Math.Round((positionX - before.Left) * (double)after.Width / before.Width),
                after.Top + (int)Math.Round((positionY - before.Top) * (double)after.Height / before.Height));
        }

        private static void ApplyProperties(Entity target, IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
        {
            foreach (string key in before.Keys.Union(after.Keys, StringComparer.Ordinal))
            {
                before.TryGetValue(key, out string? previous);
                after.TryGetValue(key, out string? value);
                if (previous == value)
                {
                    continue;
                }

                List<ListDisplayAttribute> typed = target.Properties.OfType<ListDisplayAttribute>()
                    .Where(property => string.Equals(property.DisplayName, key, StringComparison.OrdinalIgnoreCase)).ToList();
                if (typed.Count > 1 || target.Properties.OfType<CustomStringDisplayAttribute>()
                    .Count(property => (property.Value as string ?? string.Empty).StartsWith(key + ":", StringComparison.OrdinalIgnoreCase)) > 1)
                {
                    throw new NotSupportedException("The native document has ambiguous definitions for '" + key + "'. Resolve them in MTMT before editing that property.");
                }

                if (value != null && typed.Any(property => property.Value is not string[] options
                    || !options.Contains(value, StringComparer.OrdinalIgnoreCase)))
                {
                    throw new NotSupportedException("The native template does not support value '" + value + "' for '" + key + "'.");
                }

                if (value != null)
                {
                    DiagramElementHelper.SetCustomProperty(target, key, value);
                    continue;
                }

                foreach (ListDisplayAttribute property in typed)
                {
                    string[] options = (string[])property.Value!;
                    int unset = Array.FindIndex(options, option => string.Equals(option, "Select", StringComparison.OrdinalIgnoreCase));
                    if (unset < 0)
                    {
                        throw new NotSupportedException("The native template does not allow clearing '" + key + "'.");
                    }

                    property.SelectedIndex = unset;
                }

                foreach (CustomStringDisplayAttribute property in target.Properties.OfType<CustomStringDisplayAttribute>()
                    .Where(property => (property.Value as string ?? string.Empty).StartsWith(key + ":", StringComparison.OrdinalIgnoreCase)).ToArray())
                {
                    target.Properties.Remove(property);
                }
            }
        }

        private static void ApplyThreats(ThreatModel native, IReadOnlyList<ThreatStateDto>? baseline, IReadOnlyList<ThreatStateDto>? edited, ThreatModel requested)
        {
            if (Same(baseline, edited))
            {
                return;
            }

            Dictionary<string, ThreatStateDto> before = (baseline ?? Array.Empty<ThreatStateDto>()).ToDictionary(threat => threat.Id, StringComparer.Ordinal);
            Dictionary<string, ThreatStateDto> after = (edited ?? Array.Empty<ThreatStateDto>()).ToDictionary(threat => threat.Id, StringComparer.Ordinal);
            foreach (string id in before.Keys.Union(after.Keys, StringComparer.Ordinal))
            {
                before.TryGetValue(id, out ThreatStateDto? oldEntry);
                after.TryGetValue(id, out ThreatStateDto? newEntry);
                if (Same(oldEntry, newEntry))
                {
                    continue;
                }

                KeyValuePair<string, Threat>[] matches = native.AllThreatsDictionary
                    .Where(pair => pair.Key == id || pair.Value.InteractionKey == id).ToArray();
                if (matches.Length > 1)
                {
                    throw new NotSupportedException("The native register contains an ambiguous threat identity: " + id);
                }

                bool manual = ManualThreatId.IsManual(id);
                if (newEntry == null && manual)
                {
                    native.AllThreatsDictionary.Remove(matches.Single().Key);
                    continue;
                }

                newEntry ??= new ThreatStateDto { Id = id };
                if (newEntry.Manual == true && !manual)
                {
                    throw new NotSupportedException("A generated native threat cannot be changed into a manual threat.");
                }

                if (matches.Length == 0)
                {
                    if (!manual || !ManualThreatId.TryCanonicalize(id, out string? canonical, out _) || canonical != id)
                    {
                        throw new NotSupportedException("This generated threat is not in the original register. Add a manual threat or explicitly export a converted model to materialize new analysis results.");
                    }

                    Threat added = requested.AllThreatsDictionary[id];
                    ValidateThreatScope(native, added);
                    added.Id = native.AllThreatsDictionary.Count == 0 ? 1 : native.AllThreatsDictionary.Values.Max(threat => threat.Id) + 1;
                    native.AllThreatsDictionary.Add(id, added);
                    continue;
                }

                Threat target = matches[0].Value;
                oldEntry ??= new ThreatStateDto { Id = id };
                if (oldEntry.State != newEntry.State)
                {
                    target.State = ThreatStateWire.Parse(newEntry.State);
                }

                if (oldEntry.Justification != newEntry.Justification)
                {
                    target.StateInformation = newEntry.Justification ?? string.Empty;
                }

                if (oldEntry.Title != newEntry.Title)
                {
                    target.Title = newEntry.Title ?? (manual ? string.Empty : GeneratedDefault(target, "GeneratedDefaultTitle"));
                    SetThreatProperty(target, "TitleOverride", manual || newEntry.Title == null ? null : "true");
                }

                if (oldEntry.Priority != newEntry.Priority)
                {
                    target.Priority = newEntry.Priority ?? (manual ? string.Empty : GeneratedDefault(target, "GeneratedDefaultPriority"));
                    SetThreatProperty(target, "PriorityOverride", manual || newEntry.Priority == null ? null : "true");
                }

                if (oldEntry.Category != newEntry.Category)
                {
                    if (!manual)
                    {
                        throw new NotSupportedException("The category of a generated native threat belongs to its template.");
                    }

                    target.UserThreatCategory = newEntry.Category;
                }

                if (oldEntry.Description != newEntry.Description)
                {
                    target.UserThreatDescription = newEntry.Description ?? string.Empty;
                }

                if (oldEntry.Mitigation != newEntry.Mitigation)
                {
                    SetThreatProperty(target, "Mitigation", newEntry.Mitigation);
                }

                if (!Same(oldEntry.ElementIds, newEntry.ElementIds) || !Same(oldEntry.Source, newEntry.Source))
                {
                    throw new NotSupportedException("Changing the scope or provenance of an existing native threat is not supported.");
                }

                target.ModifiedAt = DateTime.UtcNow;
            }
        }

        private static void ValidateThreatScope(ThreatModel model, Threat threat)
        {
            HashSet<Guid> ids = model.DrawingSurfaceList.SelectMany(page => page.Borders.Keys.Concat(page.Lines.Keys)).ToHashSet();
            if (new[] { threat.SourceGuid, threat.TargetGuid, threat.FlowGuid }.Any(id => id != Guid.Empty && !ids.Contains(id)))
            {
                throw new NotSupportedException("A new native threat must reference objects present in the document.");
            }
        }

        private static string GeneratedDefault(Threat threat, string key)
        {
            if (threat.Properties?.TryGetValue(key, out string? value) == true)
            {
                return value;
            }

            throw new NotSupportedException("The original template default is unavailable; reset this threat in MTMT instead of discarding its authored value.");
        }

        private static void SetThreatProperty(Threat threat, string key, string? value)
        {
            if (value == null)
            {
                threat.Properties?.Remove(key);
            }
            else
            {
                threat.Properties ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                threat.Properties[key] = value;
            }
        }

        private static bool Same<T>(T before, T after) => JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before), JsonSerializer.SerializeToElement(after));

        private static XDocument ReadXml(byte[] content)
        {
            using MemoryStream stream = new MemoryStream(content, writable: false);
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = JsonDocumentPreflight.MaxBytes,
                CloseInput = false,
            };
            try
            {
                using (XmlReader probe = XmlReader.Create(stream, settings))
                {
                    while (probe.Read())
                    {
                        if (probe.Depth > 128)
                        {
                            throw new InvalidDataException("Native TM7 XML exceeds the depth limit of 128.");
                        }
                    }
                }

                stream.Position = 0;
                using XmlReader reader = XmlReader.Create(stream, settings);
                return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }
            catch (XmlException error)
            {
                throw new InvalidDataException("The native TM7 XML is invalid: " + error.Message, error);
            }
        }

        private static XDocument Serialize(ThreatModel model)
        {
            using MemoryStream output = new MemoryStream();
            model.Save(output);
            return ReadXml(output.ToArray());
        }

        private static void Patch(XElement retained, XElement before, XElement after)
        {
            if (XNode.DeepEquals(before, after))
            {
                return;
            }

            foreach (XName name in before.Attributes().Concat(after.Attributes()).Where(attribute => !attribute.IsNamespaceDeclaration).Select(attribute => attribute.Name).Distinct())
            {
                if (before.Attribute(name)?.Value != after.Attribute(name)?.Value)
                {
                    if (name == Instance + "type" && after.Attribute(name) is XAttribute type)
                    {
                        string[] parts = type.Value.Split(':');
                        XNamespace typeNamespace = parts.Length == 2
                            ? after.GetNamespaceOfPrefix(parts[0]) ?? throw new InvalidDataException("Unresolved XML type namespace.")
                            : after.GetDefaultNamespace();
                        string prefix = retained.GetPrefixOfNamespace(typeNamespace) ?? string.Empty;
                        if (prefix.Length == 0)
                        {
                            int suffix = 0;
                            do
                            {
                                prefix = "native" + suffix++.ToString(CultureInfo.InvariantCulture);
                            }
                            while (retained.GetNamespaceOfPrefix(prefix) != null);
                            retained.SetAttributeValue(XNamespace.Xmlns + prefix, typeNamespace.NamespaceName);
                        }

                        retained.SetAttributeValue(name, prefix + ":" + parts[^1]);
                    }
                    else
                    {
                        retained.SetAttributeValue(name, after.Attribute(name)?.Value);
                    }
                }
            }

            if (!before.HasElements && !after.HasElements)
            {
                if (before.Value != after.Value)
                {
                    if (retained.HasElements)
                    {
                        throw new NotSupportedException("An edited native value contains unrecognized XML that cannot be replaced safely.");
                    }

                    retained.Value = after.Value;
                }

                return;
            }

            Dictionary<string, XElement> oldChildren = Children(before);
            Dictionary<string, XElement> newChildren = Children(after);
            Dictionary<string, XElement> retainedChildren = Children(retained);
            foreach (KeyValuePair<string, XElement> child in oldChildren)
            {
                if (!retainedChildren.TryGetValue(child.Key, out XElement? originalChild))
                {
                    if (newChildren.TryGetValue(child.Key, out XElement? unchanged) && XNode.DeepEquals(child.Value, unchanged))
                    {
                        continue;
                    }

                    throw new NotSupportedException("The native XML identity of an edited field could not be matched safely.");
                }

                if (newChildren.TryGetValue(child.Key, out XElement? replacement))
                {
                    Patch(originalChild, child.Value, replacement);
                }
                else
                {
                    originalChild.Remove();
                    retainedChildren.Remove(child.Key);
                }
            }

            XElement? previous = null;
            foreach (KeyValuePair<string, XElement> child in newChildren)
            {
                if (!oldChildren.ContainsKey(child.Key))
                {
                    XElement added = CopyWithNamespaces(child.Value);
                    if (previous != null)
                    {
                        previous.AddAfterSelf(added);
                    }
                    else
                    {
                        retained.AddFirst(added);
                    }

                    retainedChildren[child.Key] = added;
                }

                retainedChildren.TryGetValue(child.Key, out previous);
            }

            if (!oldChildren.Keys.Where(newChildren.ContainsKey).SequenceEqual(newChildren.Keys.Where(oldChildren.ContainsKey)))
            {
                List<XElement> ordered = newChildren.Keys.Where(retainedChildren.ContainsKey).Select(key => retainedChildren[key]).ToList();
                List<XComment> slots = new List<XComment>();
                foreach (XElement child in retained.Elements().Where(ordered.Contains).ToArray())
                {
                    XComment slot = new XComment("native-order");
                    child.ReplaceWith(slot);
                    slots.Add(slot);
                }

                for (int index = 0; index < slots.Count; index++)
                {
                    slots[index].ReplaceWith(ordered[index]);
                }
            }
        }

        private static Dictionary<string, XElement> Children(XElement parent)
        {
            Dictionary<string, XElement> children = new Dictionary<string, XElement>(StringComparer.Ordinal);
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (XElement child in parent.Elements())
            {
                string identity = child.Name.ToString();
                XElement? key = child.Elements().FirstOrDefault(element => element.Name == child.Name.Namespace + "Key")
                    ?? child.Elements().FirstOrDefault(element => element.Name.LocalName == "Guid" && element.Name.NamespaceName == "http://schemas.datacontract.org/2004/07/ThreatModeling.Model.Abstracts");
                if (key != null)
                {
                    identity += ":" + (Guid.TryParse(key.Value, out Guid guid) ? guid.ToString("D") : key.Value);
                }
                else if (child.Name.LocalName == "anyType")
                {
                    XNamespace knowledge = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase";
                    string name = child.Element(knowledge + "Name")?.Value ?? string.Empty;
                    string display = child.Element(knowledge + "DisplayName")?.Value ?? string.Empty;
                    string value = child.Element(knowledge + "Value")?.Value ?? string.Empty;
                    string type = child.Attribute(Instance + "type")?.Value.Split(':').Last() ?? string.Empty;
                    identity += ":" + type + ":" + name + ":" + display;
                    if (type == "CustomStringDisplayAttribute")
                    {
                        identity += ":" + value.Split(':')[0];
                    }
                }

                counts.TryGetValue(identity, out int occurrence);
                counts[identity] = occurrence + 1;
                children.Add(identity + "#" + occurrence.ToString(CultureInfo.InvariantCulture), child);
            }

            return children;
        }

        private static XElement CopyWithNamespaces(XElement source)
        {
            XElement copy = new XElement(source);
            foreach (XElement ancestor in source.Ancestors())
            {
                foreach (XAttribute declaration in ancestor.Attributes().Where(attribute => attribute.IsNamespaceDeclaration))
                {
                    if (copy.Attribute(declaration.Name) == null)
                    {
                        copy.Add(new XAttribute(declaration));
                    }
                }
            }

            return copy;
        }
    }
}
