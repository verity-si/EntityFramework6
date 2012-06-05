﻿namespace System.Data.Entity.Core.Mapping.Update.Internal
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Data.Common;
    using System.Data.Entity.Core.EntityClient;
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Data.Entity.Core.Objects;
    using System.Linq;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public class FunctionUpdateCommandTests
    {
        public class Execute
        {
            [Fact]
            public void Returns_rows_affected_when_there_are_no_result_columns()
            {
                var stateEntries = new ReadOnlyCollection<IEntityStateEntry>(new List<IEntityStateEntry>());
                var stateEntry = new ExtractedStateEntry(EntityState.Unchanged, PropagatorResult.CreateSimpleValue(PropagatorFlags.NoFlags, value: 0),
                    PropagatorResult.CreateSimpleValue(PropagatorFlags.NoFlags, value: 0), new Mock<IEntityStateEntry>(MockBehavior.Strict).Object);

                int timeout = 43;
                var mockUpdateTranslator = new Mock<UpdateTranslator>(MockBehavior.Strict);
                mockUpdateTranslator.Setup(m => m.CommandTimeout).Returns(timeout);
                var entityConnection = new Mock<EntityConnection>().Object;
                mockUpdateTranslator.Setup(m => m.Connection).Returns(entityConnection);

                var mockDbCommand = new Mock<DbCommand>();

                var mockFunctionUpdateCommand = new Mock<FunctionUpdateCommand>(mockUpdateTranslator.Object, stateEntries, stateEntry, mockDbCommand.Object)
                {
                    CallBase = true
                };

                int timesCommandTimeoutCalled = 0;
                mockDbCommand.SetupSet(m => m.CommandTimeout = It.IsAny<int>()).Callback((int value) =>
                {
                    timesCommandTimeoutCalled++;
                    Assert.Equal(timeout, value);
                });

                int rowsAffected = 36;
                mockDbCommand.Setup(m => m.ExecuteNonQuery()).Returns(rowsAffected);

                int timesSetInputIdentifiers = 0;
                var identifierValues = new Dictionary<int, object>();
                mockFunctionUpdateCommand.Setup(m => m.SetInputIdentifiers(It.IsAny<Dictionary<int, object>>()))
                    .Callback<Dictionary<int, object>>(identifierValuesPassed =>
                    {
                        timesSetInputIdentifiers++;
                        Assert.Same(identifierValues, identifierValuesPassed);
                    });

                var generatedValues = new List<KeyValuePair<PropagatorResult, object>>();

                var rowsAffectedResult = mockFunctionUpdateCommand.Object.Execute(identifierValues, generatedValues);

                Assert.Equal(rowsAffected, rowsAffectedResult);
                Assert.Equal(1, timesCommandTimeoutCalled);
                Assert.Equal(1, timesSetInputIdentifiers);
                Assert.Equal(0, generatedValues.Count);
            }

            [Fact]
            public void Returns_rows_affected_when_there_are_result_columns()
            {
                var mockPrimitiveType = new Mock<PrimitiveType>();
                mockPrimitiveType.Setup(m => m.BuiltInTypeKind).Returns(BuiltInTypeKind.PrimitiveType);
                mockPrimitiveType.Setup(m => m.PrimitiveTypeKind).Returns(PrimitiveTypeKind.Int32);
                mockPrimitiveType.Setup(m => m.DataSpace).Returns(DataSpace.CSpace);
                var edmProperty = new EdmProperty("property", TypeUsage.Create(mockPrimitiveType.Object));

                var entityType = new EntityType("", "", DataSpace.CSpace, Enumerable.Empty<string>(), new[] { edmProperty });
                entityType.SetReadOnly();

                var stateEntry = new ExtractedStateEntry(
                        EntityState.Unchanged,
                        PropagatorResult.CreateSimpleValue(PropagatorFlags.NoFlags, value: 0),
                        PropagatorResult.CreateStructuralValue(new[] { PropagatorResult.CreateSimpleValue(PropagatorFlags.NoFlags, value: 0) },
                        entityType,
                        isModified: false),
                    new Mock<IEntityStateEntry>(MockBehavior.Strict).Object);

                var mockUpdateTranslator = new Mock<UpdateTranslator>(MockBehavior.Strict);
                mockUpdateTranslator.Setup(m => m.CommandTimeout).Returns(() => null);
                var entityConnection = new Mock<EntityConnection>().Object;
                mockUpdateTranslator.Setup(m => m.Connection).Returns(entityConnection);

                var mockDbCommand = new Mock<DbCommand>();
                var stateEntries = new ReadOnlyCollection<IEntityStateEntry>(new List<IEntityStateEntry>());

                var mockFunctionUpdateCommand = new Mock<FunctionUpdateCommand>(mockUpdateTranslator.Object, stateEntries, stateEntry, mockDbCommand.Object)
                {
                    CallBase = true
                };

                int rowsAffected = 36;
                mockDbCommand.Setup(m => m.ExecuteNonQuery()).Returns(rowsAffected);

                int dbValue = 66;
                var mockDbDataReader = new Mock<DbDataReader>();
                mockDbDataReader.Setup(m => m.GetValue(It.IsAny<int>())).Returns(dbValue);
                int rowsToRead = 2;
                mockDbDataReader.Setup(m => m.Read()).Returns(() =>
                    {
                        rowsToRead--;
                        return rowsToRead > 0;
                    });
                mockDbCommand.Protected().Setup<DbDataReader>("ExecuteDbDataReader", CommandBehavior.SequentialAccess).Returns(mockDbDataReader.Object);

                int timesSetInputIdentifiers = 0;
                var identifierValues = new Dictionary<int, object>();
                mockFunctionUpdateCommand.Setup(m => m.SetInputIdentifiers(It.IsAny<Dictionary<int, object>>()))
                    .Callback<Dictionary<int, object>>(identifierValuesPassed =>
                    {
                        timesSetInputIdentifiers++;
                        Assert.Same(identifierValues, identifierValuesPassed);
                    });

                var generatedValues = new List<KeyValuePair<PropagatorResult, object>>();
                var mockObjectStateManager = new Mock<ObjectStateManager>();
                var mockObjectStateEntry = new Mock<ObjectStateEntry>(mockObjectStateManager.Object, null, EntityState.Unchanged);
                var mockCurrentValueRecord = new Mock<CurrentValueRecord>(mockObjectStateEntry.Object);

                var idColumn = new KeyValuePair<string, PropagatorResult>("ID",
                    PropagatorResult.CreateServerGenSimpleValue(PropagatorFlags.NoFlags, /*value:*/ 0, mockCurrentValueRecord.Object, recordOrdinal: 0));
                mockFunctionUpdateCommand.Protected().Setup<List<KeyValuePair<string, PropagatorResult>>>("ResultColumns")
                    .Returns((new[] { idColumn }).ToList());

                var rowsAffectedResult = mockFunctionUpdateCommand.Object.Execute(identifierValues, generatedValues);

                Assert.Equal(1, rowsAffectedResult);
                Assert.Equal(1, timesSetInputIdentifiers);
                Assert.Equal(1, generatedValues.Count);
                Assert.Same(idColumn.Value, generatedValues[0].Key);
                Assert.Equal(dbValue, generatedValues[0].Value);
            }
        }
    }
}
