using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using Dreamteck.Splines.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.IO.CSV. Files live in a per-test temporary directory that TearDown removes; computers are owned game
    // objects destroyed there too. Expected values are derived from the x86-64 bodies (addresses in each comment) and worked by
    // hand: row counts from the column widths (3, 3, 3, 3, 1, 4), titles from the string literals of the title builders.
    public sealed class OriginalDreamteckCsvTests
    {
        private const float Tolerance = 1e-4f;

        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly CSV.ColumnType[] AllColumns =
        {
            CSV.ColumnType.Position, CSV.ColumnType.Tangent, CSV.ColumnType.Tangent2,
            CSV.ColumnType.Normal, CSV.ColumnType.Size, CSV.ColumnType.Color
        };

        private readonly List<GameObject> _owned = new List<GameObject>();

        private string _directory;

        [SetUp]
        public void CreateTemporaryDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "lucid-csv-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void RemoveOwnedObjectsAndFiles()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
            if (_directory != null && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ", expected " + expected.ToString("F6") + ")");
        }

        private string WriteFile(string name, params string[] lines)
        {
            string path = Path.Combine(_directory, name);
            File.WriteAllLines(path, lines);
            return path;
        }

        private static object Buffer(CSV csv)
        {
            return typeof(SplineParser).GetField("buffer", InstanceAny).GetValue(csv);
        }

        private static object Field(object target, string name)
        {
            FieldInfo info = target.GetType().GetField(name, InstanceAny);
            Assert.That(info, Is.Not.Null, name);
            return info.GetValue(target);
        }

        private static List<SplinePoint> Points(CSV csv)
        {
            return (List<SplinePoint>)Field(Buffer(csv), "points");
        }

        private SplineComputer NewComputer(string name, Spline.Type type, params Vector3[] positions)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            SplineComputer computer = owner.AddComponent<SplineComputer>();
            computer.type = type;
            computer.SetPoints(positions.Select(p => new SplinePoint(p)).ToArray());
            return computer;
        }

        // Public surface of the enum: Position .. Color keep the values 0 .. 5 (the jump tables of Read and Write dispatch on
        // them).
        [Test]
        public void ColumnTypeKeepsTheShippedValues()
        {
            Assert.That(Enum.GetUnderlyingType(typeof(CSV.ColumnType)), Is.EqualTo(typeof(int)));
            Assert.That(Enum.GetNames(typeof(CSV.ColumnType)), Is.EqualTo(new[] { "Position", "Tangent", "Tangent2", "Normal", "Size", "Color" }));
            Assert.That(Enum.GetValues(typeof(CSV.ColumnType)).Cast<CSV.ColumnType>().Select(v => (int)v), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(typeof(CSV.ColumnType).IsNestedPublic, Is.True);
        }

        // Constructor 0x4ec1d0: File.Exists first; a missing file leaves the field initialisers (an empty column list, no
        // buffer) and every use of the buffer faults.
        [Test]
        public void MissingFileLeavesAnEmptyCsvWhoseBufferMembersFault()
        {
            CSV csv = new CSV(Path.Combine(_directory, "absent.csv"));
            Assert.That(csv.columns, Is.Empty);
            Assert.That(csv.name, Is.EqualTo(string.Empty));
            Assert.That(Buffer(csv), Is.Null);
            Assert.Throws<NullReferenceException>(() => csv.CreateSpline());
            Assert.Throws<NullReferenceException>(() => csv.CreateSplineComputer(Vector3.zero, Quaternion.identity));
            Assert.Throws<NullReferenceException>(() => csv.FlatX());
        }

        // The name is taken from the path before the extension is checked, so a wrong extension keeps the name, logs the
        // error and stops before reading anything.
        [Test]
        public void WrongExtensionKeepsTheNameLogsAnErrorAndReadsNothing()
        {
            string path = WriteFile("shape.txt", "Title", "1,2,3");
            LogAssert.Expect(LogType.Error, "CSV Parsing ERROR: Wrong format. Please use SVG or XML");
            CSV csv = new CSV(path);
            Assert.That(csv.name, Is.EqualTo("shape"));
            Assert.That(csv.columns, Is.Empty);
            Assert.That(Buffer(csv), Is.Null);
        }

        // The extension is lower-cased before the comparison, so ".CSV" is accepted.
        [Test]
        public void UppercaseExtensionIsAccepted()
        {
            string path = WriteFile("SHAPE.CSV", "Title");
            CSV csv = new CSV(path);
            Assert.That(csv.name, Is.EqualTo("SHAPE"));
            Assert.That(Buffer(csv), Is.Not.Null);
            Assert.That(Points(csv), Is.Empty, "only the title row");
            Assert.That(csv.columns, Is.EqualTo(AllColumns));
        }

        // Read 0x4ec7f0 with the default columns: 3 + 3 + 3 + 3 + 1 + 4 = 17 values per row. Each group fills the pen state of
        // the buffer, then CreateSmooth adds a SmoothMirrored point built from position, tangent, normal, size and color; the
        // Tangent2 column only reaches the pen field (the point mirrors tangent2 from the tangent). The buffer type is
        // CatmullRom and whitespace inside a row is removed first.
        [Test]
        public void DefaultColumnsFillThePenStateAndTheTangent2ColumnIsNotUsedForThePoint()
        {
            string path = WriteFile("full.csv", "Title", "1, 2, 3,4,5,6,7,8,9,0,0,1,2,0.25,0.5,0.75,1");
            CSV csv = new CSV(path);
            object buffer = Buffer(csv);
            Assert.That(Field(buffer, "type"), Is.EqualTo(Spline.Type.CatmullRom));
            Assert.That(Field(buffer, "name"), Is.EqualTo("full"));
            Assert.That(Points(csv).Count, Is.EqualTo(1));
            SplinePoint point = Points(csv)[0];
            Near(point.position, new Vector3(1f, 2f, 3f), "position");
            Near(point.tangent, new Vector3(4f, 5f, 6f), "tangent");
            Near(point.tangent2, new Vector3(-2f, -1f, 0f), "tangent2 = 2 * position - tangent");
            Near(point.normal, new Vector3(0f, 0f, 1f), "normal");
            Assert.That(point.size, Is.EqualTo(2f));
            Assert.That(point.color, Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)));
            Assert.That(point.type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            Assert.That(point.isDirty, Is.True);
            Assert.That(Field(buffer, "tangent2"), Is.EqualTo(new Vector3(7f, 8f, 9f)), "the column was read into the pen");
        }

        // A custom column list is copied (a new list with the same order) and decides both the row width and the order of the
        // groups: Size then Position is 1 + 3 values, and the untouched pen tangent is zero.
        [Test]
        public void CustomColumnsAreCopiedAndReadInTheGivenOrder()
        {
            string path = WriteFile("custom.csv", "S,X,Y,Z", "3,1,2,3", "4,5,6,7");
            List<CSV.ColumnType> custom = new List<CSV.ColumnType> { CSV.ColumnType.Size, CSV.ColumnType.Position };
            CSV csv = new CSV(path, custom);
            Assert.That(csv.columns, Is.Not.SameAs(custom));
            Assert.That(csv.columns, Is.EqualTo(custom));
            Assert.That(Points(csv).Count, Is.EqualTo(2));
            Assert.That(Points(csv)[0].size, Is.EqualTo(3f));
            Near(Points(csv)[0].position, new Vector3(1f, 2f, 3f), "position of row 1");
            Near(Points(csv)[0].tangent2, new Vector3(2f, 4f, 6f), "a zero tangent mirrors to 2 * position");
            Assert.That(Points(csv)[1].size, Is.EqualTo(4f));
            Near(Points(csv)[1].position, new Vector3(5f, 6f, 7f), "position of row 2");
        }

        // Rows with the wrong number of values log the exact message (the row counts from 1 after the title row, then the
        // expected and found counts) and add nothing; later rows are still read.
        [Test]
        public void RowsWithTheWrongElementCountLogAndAreSkipped()
        {
            string path = WriteFile("short.csv", "Title", "1,2,3,4,5,6,7,8,9,0,0,1,2,0.25,0.5,0.75,1", "1,2,3", "9,8,7,6,5,4,3,2,1,0,0,1,1,1,1,1,1");
            LogAssert.Expect(LogType.Error, "Unexpected element count on row 2. Expected 17 found 3 Please make sure that all values exist and the column order is correct.");
            CSV csv = new CSV(path);
            Assert.That(Points(csv).Count, Is.EqualTo(2));
            Near(Points(csv)[0].position, new Vector3(1f, 2f, 3f), "row 1");
            Near(Points(csv)[1].position, new Vector3(9f, 8f, 7f), "row 3");
        }

        // Numbers use the en-US culture and NumberStyles.Any, and a value that does not parse is stored as 0.
        [Test]
        public void ValuesParseWithTheEnglishCultureAndFailuresBecomeZero()
        {
            string path = WriteFile("culture.csv", "S,X,Y,Z", "1.5,2.25,x,-4");
            CSV csv = new CSV(path, new List<CSV.ColumnType> { CSV.ColumnType.Size, CSV.ColumnType.Position });
            Assert.That(Points(csv)[0].size, Is.EqualTo(1.5f));
            Near(Points(csv)[0].position, new Vector3(2.25f, 0f, -4f), "position with a failed component");
        }

        // The style is NumberStyles.Any (0x1ff, stored by the constructors at 0x4ebc39 and 0x4ec2ee, passed at 0x4ecad1), so a
        // currency symbol and parentheses parse; NumberStyles.Float would fail both and store 0.
        [Test]
        public void ValuesParseWithNumberStylesAny()
        {
            string path = WriteFile("styles.csv", "S,X,Y,Z", "$1.5,(2),1e1,3");
            CSV csv = new CSV(path, new List<CSV.ColumnType> { CSV.ColumnType.Size, CSV.ColumnType.Position });
            Assert.That(Points(csv)[0].size, Is.EqualTo(1.5f));
            Near(Points(csv)[0].position, new Vector3(-2f, 10f, 3f), "position");
        }

        // Constructor 0x4ebb50 (SplineComputer): the definition takes the computer's name and points, exports Position, Tangent
        // and Tangent2, and keeps the type for Linear and Bezier splines.
        [Test]
        public void ComputerConstructorExportsPositionTangentAndTangent2()
        {
            SplineComputer computer = NewComputer("csv-source", Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f));
            CSV csv = new CSV(computer);
            Assert.That(csv.name, Is.EqualTo("csv-source"));
            Assert.That(csv.columns, Is.EqualTo(new[] { CSV.ColumnType.Position, CSV.ColumnType.Tangent, CSV.ColumnType.Tangent2 }));
            object buffer = Buffer(csv);
            Assert.That(Field(buffer, "name"), Is.EqualTo("csv-source"));
            Assert.That(Field(buffer, "type"), Is.EqualTo(Spline.Type.Linear));
            Assert.That(Field(buffer, "closed"), Is.False);
            Assert.That(Points(csv).Count, Is.EqualTo(3));
            Near(Points(csv)[1].position, new Vector3(10f, 0f, 0f), "point 1");
            Near(Points(csv)[2].tangent, new Vector3(10f, 0f, 10f), "a Linear spline keeps its tangents");
        }

        // The tangents are only rewritten for types other than Bezier and Linear: Catmull-Rom points get Bezier tangents
        // (SmoothMirrored, tangent2 ahead of the position) and every type other than Bezier and Linear ends up Bezier.
        [Test]
        public void ComputerConstructorConvertsCatmullRomAndBSplineToBezier()
        {
            SplineComputer cat = NewComputer("cat", Spline.Type.CatmullRom, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f));
            CSV catCsv = new CSV(cat);
            Assert.That(Field(Buffer(catCsv), "type"), Is.EqualTo(Spline.Type.Bezier));
            Assert.That(Points(catCsv)[1].type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            // The Catmull-Rom tangent at point 1 is parallel to (point 2 - point 0) = (10, 0, 10); tangent2 sits ahead along it.
            Vector3 ahead = Points(catCsv)[1].tangent2 - Points(catCsv)[1].position;
            Assert.That(ahead.x, Is.GreaterThan(0.1f), "tangent2 is moved ahead of the position");
            Assert.That(ahead.z, Is.EqualTo(ahead.x).Within(Tolerance), "along the neighbour chord");
            Assert.That(ahead.y, Is.EqualTo(0f).Within(Tolerance));

            SplineComputer bspline = NewComputer("bspline", Spline.Type.BSpline, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f));
            CSV bsplineCsv = new CSV(bspline);
            Assert.That(Field(Buffer(bsplineCsv), "type"), Is.EqualTo(Spline.Type.Bezier));
            Near(Points(bsplineCsv)[1].tangent2, new Vector3(10f, 0f, 0f), "a B-spline only changes type");

            SplineComputer bezier = NewComputer("bezier", Spline.Type.Bezier, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f));
            Assert.That(Field(Buffer(new CSV(bezier)), "type"), Is.EqualTo(Spline.Type.Bezier));
        }

        [Test]
        public void ClosedComputerExportsAClosedDefinitionThatCreatesAClosedSpline()
        {
            SplineComputer computer = NewComputer("closed", Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f));
            computer.Close();
            CSV csv = new CSV(computer);
            Assert.That(Field(Buffer(csv), "closed"), Is.True);
            Spline spline = csv.CreateSpline();
            Assert.That(spline.isClosed, Is.True);
            Assert.That(spline.points.Length, Is.EqualTo(3));
            Assert.That(spline.type, Is.EqualTo(Spline.Type.Linear));
            SplineComputer rebuilt = csv.CreateSplineComputer(new Vector3(1f, 2f, 3f), Quaternion.identity);
            _owned.Add(rebuilt.gameObject);
            Assert.That(rebuilt.name, Is.EqualTo("closed"));
            Assert.That(rebuilt.isClosed, Is.True);
            Near(rebuilt.transform.position, new Vector3(1f, 2f, 3f), "position");
        }

        // FlatX 0x4ed860, FlatY 0x4eda10 and FlatZ 0x4edb80 call FlattenVector on position and both tangents of every point and
        // drop the results, so the buffer is unchanged.
        [Test]
        public void FlatAxesLeaveTheBufferUnchanged()
        {
            SplineComputer computer = NewComputer("flat", Spline.Type.Linear, new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f));
            CSV csv = new CSV(computer);
            SplinePoint[] before = Points(csv).ToArray();
            csv.FlatX();
            csv.FlatY();
            csv.FlatZ();
            SplinePoint[] after = Points(csv).ToArray();
            Assert.That(after.Length, Is.EqualTo(2));
            for (int i = 0; i < before.Length; i++)
            {
                Near(after[i].position, before[i].position, "position " + i);
                Near(after[i].tangent, before[i].tangent, "tangent " + i);
                Near(after[i].tangent2, before[i].tangent2, "tangent2 " + i);
            }
        }

        // Write 0x4ee260: a title row from the columns (X, Y, Z suffixes, R G B A for colors), then one row per point. Integer
        // valued floats print without a decimal part in any culture.
        [Test]
        public void WriteBuildsTheTitleRowAndOneRowPerPoint()
        {
            SplineComputer computer = NewComputer("write", Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 5f));
            CSV csv = new CSV(computer);
            string path = Path.Combine(_directory, "out.csv");
            csv.Write(path);
            Assert.That(File.ReadAllLines(path), Is.EqualTo(new[]
            {
                "PositionX,PositionY,PositionZ,TangentX,TangentY,TangentZ,Tangent2X,Tangent2Y,Tangent2Z",
                "0,0,0,0,0,0,0,0,0",
                "10,0,5,10,0,5,10,0,5"
            }));

            csv.columns = new List<CSV.ColumnType>(AllColumns);
            string full = Path.Combine(_directory, "full.csv");
            csv.Write(full);
            // AddColorTitle 0x4edf10 joins the literal "B" (0x4ee02d) straight to the next prefix, so the last two titles are
            // not separated: ColorBColorA.
            Assert.That(File.ReadAllLines(full), Is.EqualTo(new[]
            {
                "PositionX,PositionY,PositionZ,TangentX,TangentY,TangentZ,Tangent2X,Tangent2Y,Tangent2Z,NormalX,NormalY,NormalZ,Size,ColorR,ColorG,ColorBColorA",
                "0,0,0,0,0,0,0,0,0,0,1,0,1,1,1,1,1",
                "10,0,5,10,0,5,10,0,5,0,1,0,1,1,1,1,1"
            }));

            csv.columns = new List<CSV.ColumnType> { CSV.ColumnType.Size, CSV.ColumnType.Normal };
            string reordered = Path.Combine(_directory, "reordered.csv");
            csv.Write(reordered);
            Assert.That(File.ReadAllLines(reordered), Is.EqualTo(new[] { "Size,NormalX,NormalY,NormalZ", "1,0,1,0", "1,0,1,0" }));
        }

        // AddFloat 0x4ee1c4 calls the parameterless Single.ToString (0x1DABB80), which uses the current culture: under de-DE
        // 0.5 is written as "0,5".
        [Test]
        public void WriteFormatsFloatsWithTheCurrentCulture()
        {
            SplineComputer computer = NewComputer("culture-write", Spline.Type.Linear, new Vector3(0.5f, 0f, 0f));
            CSV csv = new CSV(computer);
            csv.columns = new List<CSV.ColumnType> { CSV.ColumnType.Position };
            string path = Path.Combine(_directory, "german.csv");
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                csv.Write(path);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            Assert.That(File.ReadAllLines(path)[1], Is.EqualTo("0,5,0,0"));
        }

        [Test]
        public void WriteToAMissingDirectoryThrows()
        {
            SplineComputer computer = NewComputer("missing", Spline.Type.Linear, Vector3.zero, Vector3.right);
            CSV csv = new CSV(computer);
            DirectoryNotFoundException exception = Assert.Throws<DirectoryNotFoundException>(() => csv.Write(Path.Combine(_directory, "nowhere", "out.csv")));
            Assert.That(exception.Message, Is.EqualTo("The file is being saved to a non-existing directory."));
        }

        // A written file reads back with the same column list: the title row is skipped and each row restores the pen state.
        [Test]
        public void WrittenFilesReadBackWithTheSameColumns()
        {
            SplineComputer computer = NewComputer("round", Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 5f));
            CSV writer = new CSV(computer);
            string path = Path.Combine(_directory, "round.csv");
            writer.Write(path);
            CSV reader = new CSV(path, new List<CSV.ColumnType>(writer.columns));
            Assert.That(Points(reader).Count, Is.EqualTo(2));
            Near(Points(reader)[1].position, new Vector3(10f, 0f, 5f), "position");
            Near(Points(reader)[1].tangent, new Vector3(10f, 0f, 5f), "tangent");
            Assert.That(reader.name, Is.EqualTo("round"));
        }

        [Test]
        public void CsvKeepsTheShippedFieldOrder()
        {
            BindingFlags declared = InstanceAny | BindingFlags.DeclaredOnly;
            Assert.That(typeof(CSV).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "columns", "culture", "style" }));
            Assert.That(typeof(CSV).GetField("columns").IsPublic, Is.True);
            Assert.That(typeof(CSV).GetField("culture", declared).IsPrivate, Is.True);
            Assert.That(typeof(CSV).GetField("style", declared).IsPrivate, Is.True);
            Assert.That(typeof(CSV).BaseType, Is.EqualTo(typeof(SplineParser)));
        }
    }
}
