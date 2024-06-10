﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerFx.Connectors.Tabular;
using Microsoft.PowerFx.Core.Entities;
using Microsoft.PowerFx.Core.Functions.Delegation;
using Microsoft.PowerFx.Core.Tests;
using Microsoft.PowerFx.Syntax;
using Microsoft.PowerFx.Tests;
using Microsoft.PowerFx.Types;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.PowerFx.Connectors.Tests
{
    public class PowerPlatformTabularTests
    {
        private readonly ITestOutputHelper _output;

        public PowerPlatformTabularTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task SQL_CdpTabular_GetTables()
        {
            using var testConnector = new LoggingTestServer(null /* no swagger */, _output);
            var config = new PowerFxConfig(Features.PowerFxV1);
            var engine = new RecalcEngine(config);

            ConsoleLogger logger = new ConsoleLogger(_output);
            using var httpClient = new HttpClient(testConnector);
            string connectionId = "c1a4e9f52ec94d55bb82f319b3e33a6a";
            string jwt = "eyJ0eXAiOiJKV1QiL...";
            using var client = new PowerPlatformConnectorClient("firstrelease-003.azure-apihub.net", "49970107-0806-e5a7-be5e-7c60e2750f01", connectionId, () => jwt, httpClient) { SessionId = "8e67ebdc-d402-455a-b33a-304820832383" };

            ConnectorDataSource cds = new ConnectorDataSource("pfxdev-sql.database.windows.net,connectortest");

            testConnector.SetResponseFromFile(@"Responses\SQL GetDatasetsMetadata.json");
            await cds.GetDatasetsMetadataAsync(client, $"/apim/sql/{connectionId}", CancellationToken.None, logger);

            Assert.NotNull(cds.DatasetMetadata);
            Assert.Null(cds.DatasetMetadata.Blob);

            Assert.Equal("{server},{database}", cds.DatasetMetadata.DatasetFormat);
            Assert.NotNull(cds.DatasetMetadata.Tabular);
            Assert.Equal("dataset", cds.DatasetMetadata.Tabular.DisplayName);
            Assert.Equal("mru", cds.DatasetMetadata.Tabular.Source);
            Assert.Equal("Table", cds.DatasetMetadata.Tabular.TableDisplayName);
            Assert.Equal("Tables", cds.DatasetMetadata.Tabular.TablePluralName);
            Assert.Equal("single", cds.DatasetMetadata.Tabular.UrlEncoding);
            Assert.NotNull(cds.DatasetMetadata.Parameters);
            Assert.Equal(2, cds.DatasetMetadata.Parameters.Count);

            Assert.Equal("Server name.", cds.DatasetMetadata.Parameters[0].Description);
            Assert.Equal("server", cds.DatasetMetadata.Parameters[0].Name);
            Assert.True(cds.DatasetMetadata.Parameters[0].Required);
            Assert.Equal("string", cds.DatasetMetadata.Parameters[0].Type);
            Assert.Equal("double", cds.DatasetMetadata.Parameters[0].UrlEncoding);
            Assert.Null(cds.DatasetMetadata.Parameters[0].XMsDynamicValues);
            Assert.Equal("Server name", cds.DatasetMetadata.Parameters[0].XMsSummary);

            Assert.Equal("Database name.", cds.DatasetMetadata.Parameters[1].Description);
            Assert.Equal("database", cds.DatasetMetadata.Parameters[1].Name);
            Assert.True(cds.DatasetMetadata.Parameters[1].Required);
            Assert.Equal("string", cds.DatasetMetadata.Parameters[1].Type);
            Assert.Equal("double", cds.DatasetMetadata.Parameters[1].UrlEncoding);
            Assert.NotNull(cds.DatasetMetadata.Parameters[1].XMsDynamicValues);
            Assert.Equal("/v2/databases?server={server}", cds.DatasetMetadata.Parameters[1].XMsDynamicValues.Path);
            Assert.Equal("value", cds.DatasetMetadata.Parameters[1].XMsDynamicValues.ValueCollection);
            Assert.Equal("Name", cds.DatasetMetadata.Parameters[1].XMsDynamicValues.ValuePath);
            Assert.Equal("DisplayName", cds.DatasetMetadata.Parameters[1].XMsDynamicValues.ValueTitle);
            Assert.Equal("Database name", cds.DatasetMetadata.Parameters[1].XMsSummary);

            testConnector.SetResponseFromFile(@"Responses\SQL GetTables.json");
            IEnumerable<ConnectorTable> tables = await cds.GetTablesAsync(client, $"/apim/sql/{connectionId}", CancellationToken.None, logger);

            Assert.NotNull(tables);
            Assert.Equal(4, tables.Count());
            Assert.Equal("[dbo].[Customers],[dbo].[Orders],[dbo].[Products],[sys].[database_firewall_rules]", string.Join(",", tables.Select(t => t.TableName)));
            Assert.Equal("Customers,Orders,Products,sys.database_firewall_rules", string.Join(",", tables.Select(t => t.DisplayName)));

            ConnectorTable connectorTable = tables.First(t => t.DisplayName == "Customers");

            Assert.False(connectorTable.IsInitialized);
            Assert.Equal("Customers", connectorTable.DisplayName);

            testConnector.SetResponseFromFile(@"Responses\SQL Server Load Customers DB.json");
            await connectorTable.InitAsync(client, $"/apim/sql/{connectionId}", CancellationToken.None, logger);
            Assert.True(connectorTable.IsInitialized);

            ConnectorTableValue sqlTable = connectorTable.GetTableValue();
            Assert.True(sqlTable._tabularService.IsInitialized);
            Assert.True(sqlTable.IsDelegable);
            Assert.Equal("*[Address:s, Country:s, CustomerId:w, Name:s, Phone:s]", sqlTable.Type._type.ToString());

            HashSet<IExternalTabularDataSource> ads = sqlTable.Type._type.AssociatedDataSources;
            Assert.NotNull(ads);

            // Tests skipped as ConnectorType.AddDataSource is skipping the creation of AssociatedDataSources
#if false
            Assert.Single(ads);

            TabularDataSource tds = Assert.IsType<TabularDataSource>(ads.First());
            Assert.NotNull(tds);
            Assert.NotNull(tds.DataEntityMetadataProvider);

            CdpEntityMetadataProvider cemp = Assert.IsType<CdpEntityMetadataProvider>(tds.DataEntityMetadataProvider);
            Assert.True(cemp.TryGetEntityMetadata("Customers", out IDataEntityMetadata dem));

            TabularDataSourceMetadata tdsm = Assert.IsType<TabularDataSourceMetadata>(dem);
            Assert.Equal("pfxdev-sql.database.windows.net,connectortest", tdsm.DatasetName);
            Assert.Equal("Customers", tdsm.EntityName);

            Assert.Equal("Customers", tds.EntityName.Value);
            Assert.True(tds.IsDelegatable);
            Assert.True(tds.IsPageable);
            Assert.True(tds.IsRefreshable);
            Assert.True(tds.IsSelectable);
            Assert.True(tds.IsWritable);
            Assert.Equal(DataSourceKind.Connected, tds.Kind);
            Assert.Equal("Customers", tds.Name);
            Assert.True(tds.RequiresAsync);
            Assert.NotNull(tds.ServiceCapabilities);
#endif

            Assert.NotNull(sqlTable._connectorType);
            Assert.Null(sqlTable._connectorType.Relationships);
            
#pragma warning disable CS0618 // Type or member is obsolete

            // Enable IR rewritter to auto-inject ServiceProvider where needed
            engine.EnableTabularConnectors();

#pragma warning restore CS0618 // Type or member is obsolete

            SymbolValues symbolValues = new SymbolValues().Add("Customers", sqlTable);
            RuntimeConfig rc = new RuntimeConfig(symbolValues)
                                    .AddService<ConnectorLogger>(logger)
                                    .AddService<HttpClient>(client);

            // Expression with tabular connector
            string expr = @"First(Customers).Address";
            CheckResult check = engine.Check(expr, options: new ParserOptions() { AllowsSideEffects = true }, symbolTable: symbolValues.SymbolTable);
            Assert.True(check.IsSuccess);

            // Confirm that InjectServiceProviderFunction has properly been added
            string ir = new Regex("RuntimeValues_[0-9]+").Replace(check.PrintIR(), "RuntimeValues_XXX");
            Assert.Equal("FieldAccess(First:![Address:s, Country:s, CustomerId:w, Name:s, Phone:s](InjectServiceProviderFunction:*[Address:s, Country:s, CustomerId:w, Name:s, Phone:s](ResolvedObject('Customers:RuntimeValues_XXX'))), Address)", ir);

            // Use tabular connector. Internally we'll call ConnectorTableValueWithServiceProvider.GetRowsInternal to get the data
            testConnector.SetResponseFromFile(@"Responses\SQL Server Get First Customers.json");
            FormulaValue result = await check.GetEvaluator().EvalAsync(CancellationToken.None, rc);

            StringValue address = Assert.IsType<StringValue>(result);
            Assert.Equal("Juigné", address.Value);

            // Rows are not cached here as the cache is stored in ConnectorTableValueWithServiceProvider which is created by InjectServiceProviderFunction, itself added during Engine.Check
            testConnector.SetResponseFromFile(@"Responses\SQL Server Get First Customers.json");
            result = await engine.EvalAsync("Last(Customers).Phone", CancellationToken.None, runtimeConfig: rc);
            StringValue phone = Assert.IsType<StringValue>(result);
            Assert.Equal("+1-425-705-0000", phone.Value);
        }

        [Fact]
        public async Task SQL_CdpTabular()
        {
            using var testConnector = new LoggingTestServer(@"Swagger\SQL Server.json", _output);
            var apiDoc = testConnector._apiDocument;
            var config = new PowerFxConfig(Features.PowerFxV1);
            var engine = new RecalcEngine(config);

            using var httpClient = new HttpClient(testConnector);
            string connectionId = "18992e9477684930acd2cc5dc9bb94c2";
            string jwt = "eyJ0eXAiOiJK...";
            using var client = new PowerPlatformConnectorClient("firstrelease-003.azure-apihub.net", "49970107-0806-e5a7-be5e-7c60e2750f01", connectionId, () => jwt, httpClient)
            {
                SessionId = "8e67ebdc-d402-455a-b33a-304820832383"
            };

            // Use of tabular connector
            // There is a network call here to retrieve the table's schema
            testConnector.SetResponseFromFile(@"Responses\SQL Server Load Customers DB.json");

            ConsoleLogger logger = new ConsoleLogger(_output);
            ConnectorTable tabularService = new ConnectorTable("pfxdev-sql.database.windows.net,connectortest", "Customers");

            Assert.False(tabularService.IsInitialized);
            Assert.Equal("Customers", tabularService.TableName);

            testConnector.SetResponseFromFiles(@"Responses\SQL GetDatasetsMetadata.json", @"Responses\SQL Server Load Customers DB.json");
            await tabularService.InitAsync(client, $"/apim/sql/{connectionId}", CancellationToken.None, logger);
            Assert.True(tabularService.IsInitialized);

            ConnectorTableValue sqlTable = tabularService.GetTableValue();
            Assert.True(sqlTable._tabularService.IsInitialized);
            Assert.True(sqlTable.IsDelegable);
            Assert.Equal("*[Address:s, Country:s, CustomerId:w, Name:s, Phone:s]", sqlTable.Type._type.ToString());

#pragma warning disable CS0618 // Type or member is obsolete

            // Enable IR rewritter to auto-inject ServiceProvider where needed
            engine.EnableTabularConnectors();

#pragma warning restore CS0618 // Type or member is obsolete

            SymbolValues symbolValues = new SymbolValues().Add("Customers", sqlTable);
            RuntimeConfig rc = new RuntimeConfig(symbolValues)
                                    .AddService<ConnectorLogger>(logger)
                                    .AddService<HttpClient>(client);

            // Expression with tabular connector
            string expr = @"First(Customers).Address";
            CheckResult check = engine.Check(expr, options: new ParserOptions() { AllowsSideEffects = true }, symbolTable: symbolValues.SymbolTable);
            Assert.True(check.IsSuccess);

            // Confirm that InjectServiceProviderFunction has properly been added
            string ir = new Regex("RuntimeValues_[0-9]+").Replace(check.PrintIR(), "RuntimeValues_XXX");
            Assert.Equal("FieldAccess(First:![Address:s, Country:s, CustomerId:w, Name:s, Phone:s](InjectServiceProviderFunction:*[Address:s, Country:s, CustomerId:w, Name:s, Phone:s](ResolvedObject('Customers:RuntimeValues_XXX'))), Address)", ir);

            // Use tabular connector. Internally we'll call ConnectorTableValueWithServiceProvider.GetRowsInternal to get the data
            testConnector.SetResponseFromFile(@"Responses\SQL Server Get First Customers.json");
            FormulaValue result = await check.GetEvaluator().EvalAsync(CancellationToken.None, rc);

            StringValue address = Assert.IsType<StringValue>(result);
            Assert.Equal("Juigné", address.Value);

            // Rows are not cached here as the cache is stored in ConnectorTableValueWithServiceProvider which is created by InjectServiceProviderFunction, itself added during Engine.Check
            testConnector.SetResponseFromFile(@"Responses\SQL Server Get First Customers.json");
            result = await engine.EvalAsync("Last(Customers).Phone", CancellationToken.None, runtimeConfig: rc);
            StringValue phone = Assert.IsType<StringValue>(result);
            Assert.Equal("+1-425-705-0000", phone.Value);
        }

        [Fact]
        public async Task SP_CdpTabular_GetTables()
        {
            using var testConnector = new LoggingTestServer(null /* no swagger */, _output);
            var config = new PowerFxConfig(Features.PowerFxV1);
            var engine = new RecalcEngine(config);

            using var httpClient = new HttpClient(testConnector);
            string connectionId = "3738993883dc406d86802d8a6a923d3e";
            string jwt = "eyJ0eXAiOiJK...";
            using var client = new PowerPlatformConnectorClient("firstrelease-003.azure-apihub.net", "49970107-0806-e5a7-be5e-7c60e2750f01", connectionId, () => jwt, httpClient) { SessionId = "8e67ebdc-d402-455a-b33a-304820832383" };

            ConsoleLogger logger = new ConsoleLogger(_output);
            ConnectorDataSource cds = new ConnectorDataSource("https://microsofteur.sharepoint.com/teams/pfxtest");

            testConnector.SetResponseFromFiles(@"Responses\SP GetDatasetsMetadata.json", @"Responses\SP GetTables.json");
            IEnumerable<ConnectorTable> tables = await cds.GetTablesAsync(client, $"/apim/sharepointonline/{connectionId}", CancellationToken.None, logger);

            Assert.NotNull(cds.DatasetMetadata);

            Assert.NotNull(cds.DatasetMetadata.Blob);
            Assert.Equal("mru", cds.DatasetMetadata.Blob.Source);
            Assert.Equal("site", cds.DatasetMetadata.Blob.DisplayName);
            Assert.Equal("double", cds.DatasetMetadata.Blob.UrlEncoding);

            Assert.Null(cds.DatasetMetadata.DatasetFormat);
            Assert.Null(cds.DatasetMetadata.Parameters);

            Assert.NotNull(cds.DatasetMetadata.Tabular);
            Assert.Equal("site", cds.DatasetMetadata.Tabular.DisplayName);
            Assert.Equal("mru", cds.DatasetMetadata.Tabular.Source);
            Assert.Equal("list", cds.DatasetMetadata.Tabular.TableDisplayName);
            Assert.Equal("lists", cds.DatasetMetadata.Tabular.TablePluralName);
            Assert.Equal("double", cds.DatasetMetadata.Tabular.UrlEncoding);

            Assert.NotNull(tables);
            Assert.Equal(2, tables.Count());
            Assert.Equal("4bd37916-0026-4726-94e8-5a0cbc8e476a,5266fcd9-45ef-4b8f-8014-5d5c397db6f0", string.Join(",", tables.Select(t => t.TableName)));
            Assert.Equal("Documents,MikeTestList", string.Join(",", tables.Select(t => t.DisplayName)));

            ConnectorTable connectorTable = tables.First(t => t.DisplayName == "Documents");

            Assert.False(connectorTable.IsInitialized);
            Assert.Equal("4bd37916-0026-4726-94e8-5a0cbc8e476a", connectorTable.TableName);

            testConnector.SetResponseFromFiles(@"Responses\SP GetTable.json");
            await connectorTable.InitAsync(client, $"/apim/sharepointonline/{connectionId}", CancellationToken.None, logger);
            Assert.True(connectorTable.IsInitialized);

            ConnectorTableValue spTable = connectorTable.GetTableValue();
            Assert.True(spTable._tabularService.IsInitialized);
            Assert.True(spTable.IsDelegable);

            Assert.Equal(
                "*[Author`'Created By':![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], CheckoutUser`'Checked Out To':![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], ComplianceAssetId`'Compliance " +
                "Asset Id':s, Created:d, Editor`'Modified By':![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], ID:w, Modified:d, OData__ColorTag`'Color Tag':s, OData__DisplayName`Sensitivity:s, " +
                "OData__ExtendedDescription`Description:s, OData__ip_UnifiedCompliancePolicyProperties`'Unified Compliance Policy Properties':s, Title:s, '{FilenameWithExtension}'`'File name with extension':s, '{FullPath}'`'Full " +
                "Path':s, '{Identifier}'`Identifier:s, '{IsCheckedOut}'`'Checked out':b, '{IsFolder}'`IsFolder:b, '{Link}'`'Link to item':s, '{ModerationComment}'`'Comments associated with the content approval of this " +
                "list item':s, '{ModerationStatus}'`'Content approval status':s, '{Name}'`Name:s, '{Path}'`'Folder path':s, '{Thumbnail}'`Thumbnail:![Large:s, Medium:s, Small:s], '{TriggerWindowEndToken}'`'Trigger Window " +
                "End Token':s, '{TriggerWindowStartToken}'`'Trigger Window Start Token':s, '{VersionNumber}'`'Version number':s]", spTable.Type.ToStringWithDisplayNames());

            HashSet<IExternalTabularDataSource> ads = spTable.Type._type.AssociatedDataSources;
            Assert.NotNull(ads);

            // Tests skipped as ConnectorType.AddDataSource is skipping the creation of AssociatedDataSources
#if false
            Assert.Single(ads);
            
            TabularDataSource tds = Assert.IsType<TabularDataSource>(ads.First());
            Assert.NotNull(tds);
            Assert.NotNull(tds.DataEntityMetadataProvider);

            CdpEntityMetadataProvider cemp = Assert.IsType<CdpEntityMetadataProvider>(tds.DataEntityMetadataProvider);
            Assert.True(cemp.TryGetEntityMetadata("Documents", out IDataEntityMetadata dem));

            TabularDataSourceMetadata tdsm = Assert.IsType<TabularDataSourceMetadata>(dem);
            Assert.Equal("https://microsofteur.sharepoint.com/teams/pfxtest", tdsm.DatasetName);
            Assert.Equal("Documents", tdsm.EntityName);

            Assert.Equal("Documents", tds.EntityName.Value);
            Assert.True(tds.IsDelegatable);
            Assert.True(tds.IsPageable);
            Assert.True(tds.IsRefreshable);
            Assert.False(tds.IsSelectable);
            Assert.True(tds.IsWritable);
            Assert.Equal(DataSourceKind.Connected, tds.Kind);
            Assert.Equal("Documents", tds.Name);
            Assert.True(tds.RequiresAsync);
            Assert.NotNull(tds.ServiceCapabilities);
#endif

            Assert.NotNull(spTable._connectorType);
            Assert.NotNull(spTable._connectorType.Relationships);
            Assert.Equal(3, spTable._connectorType.Relationships.Count);
            Assert.Equal("Editor, Author, CheckoutUser", string.Join(", ", spTable._connectorType.Relationships.Select(kvp => kvp.Key)));
            Assert.Equal("Editor, Author, CheckoutUser", string.Join(", ", spTable._connectorType.Relationships.Select(kvp => kvp.Value.TargetEntity)));
            Assert.Equal("Editor#Claims-Claims, Author#Claims-Claims, CheckoutUser#Claims-Claims", string.Join(", ", spTable._connectorType.Relationships.Select(kvp => string.Join("|", kvp.Value.ReferentialConstraints.Select(kvp2 => $"{kvp2.Key}-{kvp2.Value}")))));

#pragma warning disable CS0618 // Type or member is obsolete

            // Enable IR rewritter to auto-inject ServiceProvider where needed
            engine.EnableTabularConnectors();

#pragma warning restore CS0618 // Type or member is obsolete

            SymbolValues symbolValues = new SymbolValues().Add("Documents", spTable);
            RuntimeConfig rc = new RuntimeConfig(symbolValues)
                                    .AddService<ConnectorLogger>(logger)
                                    .AddService<HttpClient>(client);

            // Expression with tabular connector
            string expr = @"First(Documents).Name";
            CheckResult check = engine.Check(expr, options: new ParserOptions() { AllowsSideEffects = true }, symbolTable: symbolValues.SymbolTable);
            Assert.True(check.IsSuccess);

            // Confirm that InjectServiceProviderFunction has properly been added
            string ir = new Regex("RuntimeValues_[0-9]+").Replace(check.PrintIR(), "RuntimeValues_XXX");
            Assert.Equal(
                "FieldAccess(First:![Author:![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], CheckoutUser:![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], " +
                "ComplianceAssetId:s, Created:d, Editor:![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], ID:w, Modified:d, OData__ColorTag:s, OData__DisplayName:s, " +
                "OData__ExtendedDescription:s, OData__ip_UnifiedCompliancePolicyProperties:s, Title:s, '{FilenameWithExtension}':s, '{FullPath}':s, '{Identifier}':s, '{IsCheckedOut}':b, '{IsFolder}':b, " +
                "'{Link}':s, '{ModerationComment}':s, '{ModerationStatus}':s, '{Name}':s, '{Path}':s, '{Thumbnail}':![Large:s, Medium:s, Small:s], '{TriggerWindowEndToken}':s, '{TriggerWindowStartToken}':s, " +
                "'{VersionNumber}':s](InjectServiceProviderFunction:*[Author:![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], CheckoutUser:![Claims:s, Department:s, DisplayName:s, " +
                "Email:s, JobTitle:s, Picture:s], ComplianceAssetId:s, Created:d, Editor:![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], ID:w, Modified:d, OData__ColorTag:s, " +
                "OData__DisplayName:s, OData__ExtendedDescription:s, OData__ip_UnifiedCompliancePolicyProperties:s, Title:s, '{FilenameWithExtension}':s, '{FullPath}':s, '{Identifier}':s, '{IsCheckedOut}':b, " +
                "'{IsFolder}':b, '{Link}':s, '{ModerationComment}':s, '{ModerationStatus}':s, '{Name}':s, '{Path}':s, '{Thumbnail}':![Large:s, Medium:s, Small:s], '{TriggerWindowEndToken}':s, " +
                "'{TriggerWindowStartToken}':s, '{VersionNumber}':s](ResolvedObject('Documents:RuntimeValues_XXX'))), {Name})", ir);

            // Use tabular connector. Internally we'll call ConnectorTableValueWithServiceProvider.GetRowsInternal to get the data
            testConnector.SetResponseFromFile(@"Responses\SP GetData.json");
            FormulaValue result = await check.GetEvaluator().EvalAsync(CancellationToken.None, rc);

            StringValue docName = Assert.IsType<StringValue>(result);
            Assert.Equal("Document1", docName.Value);
        }

        [Fact]
        public async Task SP_CdpTabular()
        {
            using var testConnector = new LoggingTestServer(null /* no swagger */, _output);
            var config = new PowerFxConfig(Features.PowerFxV1);
            var engine = new RecalcEngine(config);

            ConsoleLogger logger = new ConsoleLogger(_output);
            using var httpClient = new HttpClient(testConnector);
            string connectionId = "0b905132239e463a9d12f816be201da9";
            string jwt = "eyJ0eXAiOiJKV....";
            using var client = new PowerPlatformConnectorClient("firstrelease-003.azure-apihub.net", "49970107-0806-e5a7-be5e-7c60e2750f01", connectionId, () => jwt, httpClient)
            {
                SessionId = "8e67ebdc-d402-455a-b33a-304820832384"
            };

            ConnectorTable tabularService = new ConnectorTable("https://microsofteur.sharepoint.com/teams/pfxtest", "Documents");

            Assert.False(tabularService.IsInitialized);
            Assert.Equal("Documents", tabularService.TableName);

            testConnector.SetResponseFromFiles(@"Responses\SP GetDatasetsMetadata.json", @"Responses\SP GetTable.json");
            await tabularService.InitAsync(client, $"/apim/sharepointonline/{connectionId}", CancellationToken.None, logger);
            Assert.True(tabularService.IsInitialized);

            ConnectorTableValue spTable = tabularService.GetTableValue();
            Assert.True(spTable._tabularService.IsInitialized);
            Assert.True(spTable.IsDelegable);

            Assert.Equal(
                "*[Author`'Created By':![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], CheckoutUser`'Checked Out To':![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], ComplianceAssetId`'Compliance " +
                "Asset Id':s, Created:d, Editor`'Modified By':![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], ID:w, Modified:d, OData__ColorTag`'Color Tag':s, OData__DisplayName`Sensitivity:s, " +
                "OData__ExtendedDescription`Description:s, OData__ip_UnifiedCompliancePolicyProperties`'Unified Compliance Policy Properties':s, Title:s, '{FilenameWithExtension}'`'File name with extension':s, '{FullPath}'`'Full " +
                "Path':s, '{Identifier}'`Identifier:s, '{IsCheckedOut}'`'Checked out':b, '{IsFolder}'`IsFolder:b, '{Link}'`'Link to item':s, '{ModerationComment}'`'Comments associated with the content approval of this " +
                "list item':s, '{ModerationStatus}'`'Content approval status':s, '{Name}'`Name:s, '{Path}'`'Folder path':s, '{Thumbnail}'`Thumbnail:![Large:s, Medium:s, Small:s], '{TriggerWindowEndToken}'`'Trigger Window " +
                "End Token':s, '{TriggerWindowStartToken}'`'Trigger Window Start Token':s, '{VersionNumber}'`'Version number':s]", spTable.Type.ToStringWithDisplayNames());

#pragma warning disable CS0618 // Type or member is obsolete

            // Enable IR rewritter to auto-inject ServiceProvider where needed
            engine.EnableTabularConnectors();

#pragma warning restore CS0618 // Type or member is obsolete

            SymbolValues symbolValues = new SymbolValues().Add("Documents", spTable);
            RuntimeConfig rc = new RuntimeConfig(symbolValues)
                                    .AddService<ConnectorLogger>(logger)
                                    .AddService<HttpClient>(client);

            // Expression with tabular connector
            string expr = @"First(Documents).Name";
            CheckResult check = engine.Check(expr, options: new ParserOptions() { AllowsSideEffects = true }, symbolTable: symbolValues.SymbolTable);
            Assert.True(check.IsSuccess);

            // Confirm that InjectServiceProviderFunction has properly been added
            string ir = new Regex("RuntimeValues_[0-9]+").Replace(check.PrintIR(), "RuntimeValues_XXX");
            Assert.Equal(
                "FieldAccess(First:![Author:![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], CheckoutUser:![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], " +
                "ComplianceAssetId:s, Created:d, Editor:![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], ID:w, Modified:d, OData__ColorTag:s, OData__DisplayName:s, " +
                "OData__ExtendedDescription:s, OData__ip_UnifiedCompliancePolicyProperties:s, Title:s, '{FilenameWithExtension}':s, '{FullPath}':s, '{Identifier}':s, '{IsCheckedOut}':b, '{IsFolder}':b, " +
                "'{Link}':s, '{ModerationComment}':s, '{ModerationStatus}':s, '{Name}':s, '{Path}':s, '{Thumbnail}':![Large:s, Medium:s, Small:s], '{TriggerWindowEndToken}':s, '{TriggerWindowStartToken}':s, " +
                "'{VersionNumber}':s](InjectServiceProviderFunction:*[Author:![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], CheckoutUser:![Claims:s, Department:s, DisplayName:s, " +
                "Email:s, JobTitle:s, Picture:s], ComplianceAssetId:s, Created:d, Editor:![Claims:s, Department:s, DisplayName:s, Email:s, JobTitle:s, Picture:s], ID:w, Modified:d, OData__ColorTag:s, " +
                "OData__DisplayName:s, OData__ExtendedDescription:s, OData__ip_UnifiedCompliancePolicyProperties:s, Title:s, '{FilenameWithExtension}':s, '{FullPath}':s, '{Identifier}':s, '{IsCheckedOut}':b, " +
                "'{IsFolder}':b, '{Link}':s, '{ModerationComment}':s, '{ModerationStatus}':s, '{Name}':s, '{Path}':s, '{Thumbnail}':![Large:s, Medium:s, Small:s], '{TriggerWindowEndToken}':s, " +
                "'{TriggerWindowStartToken}':s, '{VersionNumber}':s](ResolvedObject('Documents:RuntimeValues_XXX'))), {Name})", ir);

            // Use tabular connector. Internally we'll call ConnectorTableValueWithServiceProvider.GetRowsInternal to get the data
            testConnector.SetResponseFromFile(@"Responses\SP GetData.json");
            FormulaValue result = await check.GetEvaluator().EvalAsync(CancellationToken.None, rc);

            StringValue docName = Assert.IsType<StringValue>(result);
            Assert.Equal("Document1", docName.Value);
        }

        [Fact]
        public async Task SF_CountRows()
        {
            using var testConnector = new LoggingTestServer(null /* no swagger */, _output);
            var config = new PowerFxConfig(Features.PowerFxV1);
            var engine = new RecalcEngine(config);

            ConsoleLogger logger = new ConsoleLogger(_output);
            using var httpClient = new HttpClient(testConnector);
            string connectionId = "ba3b1db7bb854aedbad2058b66e36e83";
            string jwt = "eyJ0eXAiOiJK...";
            using var client = new PowerPlatformConnectorClient("tip1002-002.azure-apihub.net", "7526ddf1-6e97-eed6-86bb-8fd46790d670", connectionId, () => jwt, httpClient) { SessionId = "8e67ebdc-d402-455a-b33a-304820832383" };

            ConnectorDataSource cds = new ConnectorDataSource("default");

            testConnector.SetResponseFromFiles(@"Responses\SF GetDatasetsMetadata.json", @"Responses\SF GetTables.json");
            IEnumerable<ConnectorTable> tables = await cds.GetTablesAsync(client, $"/apim/salesforce/{connectionId}", CancellationToken.None, logger);
            ConnectorTable connectorTable = tables.First(t => t.DisplayName == "Accounts");
            
            testConnector.SetResponseFromFile(@"Responses\SF GetSchema.json");
            await connectorTable.InitAsync(client, $"/apim/salesforce/{connectionId}", CancellationToken.None, logger);
            ConnectorTableValue sfTable = connectorTable.GetTableValue();

#pragma warning disable CS0618 // Type or member is obsolete

            // Enable IR rewritter to auto-inject ServiceProvider where needed
            engine.EnableTabularConnectors();

#pragma warning restore CS0618 // Type or member is obsolete

            SymbolValues symbolValues = new SymbolValues().Add("Accounts", sfTable);
            RuntimeConfig rc = new RuntimeConfig(symbolValues)
                                    .AddService<ConnectorLogger>(logger)
                                    .AddService<HttpClient>(client);

            // Expression with tabular connector
            string expr = @"CountRows(Accounts)";
            CheckResult check = engine.Check(expr, options: new ParserOptions() { AllowsSideEffects = true }, symbolTable: symbolValues.SymbolTable);
            Assert.True(check.IsSuccess);

            testConnector.SetResponseFromFile(@"Responses\SF GetData.json");
            FormulaValue result = await check.GetEvaluator().EvalAsync(CancellationToken.None, rc);
            Assert.Equal(6, ((DecimalValue)result).Value);
        }

        [Fact]
        public async Task SF_Filter()
        {
            using var testConnector = new LoggingTestServer(null /* no swagger */, _output);
            var config = new PowerFxConfig(Features.PowerFxV1);
            var engine = new RecalcEngine(config);

            ConsoleLogger logger = new ConsoleLogger(_output);
            using var httpClient = new HttpClient(testConnector);
            string connectionId = "ba3b1db7bb854aedbad2058b66e36e83";
            string jwt = "eyJ0eXAiOi...";
            using var client = new PowerPlatformConnectorClient("tip1002-002.azure-apihub.net", "7526ddf1-6e97-eed6-86bb-8fd46790d670", connectionId, () => jwt, httpClient) { SessionId = "8e67ebdc-d402-455a-b33a-304820832383" };

            ConnectorDataSource cds = new ConnectorDataSource("default");

            testConnector.SetResponseFromFiles(@"Responses\SF GetDatasetsMetadata.json", @"Responses\SF GetTables.json");
            IEnumerable<ConnectorTable> tables = await cds.GetTablesAsync(client, $" / apim/salesforce/{connectionId}", CancellationToken.None, logger);
            ConnectorTable connectorTable = tables.First(t => t.DisplayName == "Accounts");

            testConnector.SetResponseFromFile(@"Responses\SF GetSchema.json");
            await connectorTable.InitAsync(client, $"/apim/salesforce/{connectionId}", CancellationToken.None, logger);
            ConnectorTableValue sfTable = connectorTable.GetTableValue();

#pragma warning disable CS0618 // Type or member is obsolete

            // Enable IR rewritter to auto-inject ServiceProvider where needed
            engine.EnableTabularConnectors();

#pragma warning restore CS0618 // Type or member is obsolete

            SymbolValues symbolValues = new SymbolValues().Add("Accounts", sfTable);
            RuntimeConfig rc = new RuntimeConfig(symbolValues).AddService<ConnectorLogger>(logger).AddService<HttpClient>(client);

            // Expression with tabular connector
            string expr = @"First(Filter(Accounts, 'Account ID' = ""001DR00001Xlq74YAB"")).'Account Name'";
            CheckResult check = engine.Check(expr, options: new ParserOptions() { AllowsSideEffects = true }, symbolTable: symbolValues.SymbolTable);
            Assert.True(check.IsSuccess);

            testConnector.SetResponseFromFile(@"Responses\SF GetData.json");
            FormulaValue result = await check.GetEvaluator().EvalAsync(CancellationToken.None, rc);
            Assert.Equal("Kutch and Sons", ((StringValue)result).Value);
        }

        [Fact]
        public async Task SF_CdpTabular_GetTables()
        {
            using var testConnector = new LoggingTestServer(null /* no swagger */, _output);
            var config = new PowerFxConfig(Features.PowerFxV1);
            var engine = new RecalcEngine(config);

            ConsoleLogger logger = new ConsoleLogger(_output);
            using var httpClient = new HttpClient(testConnector);
            string connectionId = "ba3b1db7bb854aedbad2058b66e36e83";
            string jwt = "eyJ0eXAi...";
            using var client = new PowerPlatformConnectorClient("7526ddf1-6e97-eed6-86bb-8fd46790d670.05.common.tip1002.azure-apihub.net", "7526ddf1-6e97-eed6-86bb-8fd46790d670", connectionId, () => jwt, httpClient) { SessionId = "8e67ebdc-d402-455a-b33a-304820832383" };

            ConnectorDataSource cds = new ConnectorDataSource("default");

            testConnector.SetResponseFromFile(@"Responses\SF GetDatasetsMetadata.json");
            await cds.GetDatasetsMetadataAsync(client, $"/apim/salesforce/{connectionId}", CancellationToken.None, logger);

            Assert.NotNull(cds.DatasetMetadata);
            Assert.Null(cds.DatasetMetadata.Blob);
            Assert.Null(cds.DatasetMetadata.DatasetFormat);
            Assert.Null(cds.DatasetMetadata.Parameters);

            Assert.NotNull(cds.DatasetMetadata.Tabular);
            Assert.Equal("dataset", cds.DatasetMetadata.Tabular.DisplayName);
            Assert.Equal("singleton", cds.DatasetMetadata.Tabular.Source);
            Assert.Equal("Table", cds.DatasetMetadata.Tabular.TableDisplayName);
            Assert.Equal("Tables", cds.DatasetMetadata.Tabular.TablePluralName);
            Assert.Equal("double", cds.DatasetMetadata.Tabular.UrlEncoding);

            // only one network call as we already read metadata
            testConnector.SetResponseFromFile(@"Responses\SF GetTables.json");
            IEnumerable<ConnectorTable> tables = await cds.GetTablesAsync(client, $"/apim/salesforce/{connectionId}", CancellationToken.None, logger);

            Assert.NotNull(tables);
            Assert.Equal(569, tables.Count());

            ConnectorTable connectorTable = tables.First(t => t.DisplayName == "Accounts");
            Assert.Equal("Account", connectorTable.TableName);
            Assert.False(connectorTable.IsInitialized);

            testConnector.SetResponseFromFile(@"Responses\SF GetSchema.json");
            await connectorTable.InitAsync(client, $"/apim/salesforce/{connectionId}", CancellationToken.None, logger);
            Assert.True(connectorTable.IsInitialized);

            ConnectorTableValue sfTable = connectorTable.GetTableValue();
            Assert.True(sfTable._tabularService.IsInitialized);
            Assert.True(sfTable.IsDelegable);

            // Note relationships with external tables (logicalName`displayName[externalTable]:type)
            //   CreatedById`'Created By ID'[User]:s
            //   LastModifiedById`'Last Modified By ID'[User]:s            
            //   Modified By ID'[User]:s
            //   MasterRecordId`'Master Record ID'[Account]:s
            //   OwnerId`'Owner ID'[User]:s
            //   ParentId`'Parent Account ID'[Account]:s
            Assert.Equal(
                "*[AccountSource`'Account Source':s, BillingCity`'Billing City':s, BillingCountry`'Billing Country':s, BillingGeocodeAccuracy`'Billing Geocode Accuracy':s, BillingLatitude`'Billing Latitude':w, BillingLongitude`'Billing " +
                "Longitude':w, BillingPostalCode`'Billing Zip/Postal Code':s, BillingState`'Billing State/Province':s, BillingStreet`'Billing Street':s, CreatedById`'Created By ID'[User]:s, CreatedDate`'Created Date':d, " +
                "Description`'Account Description':s, Id`'Account ID':s, Industry:s, IsDeleted`Deleted:b, Jigsaw`'Data.com Key':s, JigsawCompanyId`'Jigsaw Company ID':s, LastActivityDate`'Last Activity':D, LastModifiedById`'Last " +
                "Modified By ID'[User]:s, LastModifiedDate`'Last Modified Date':d, LastReferencedDate`'Last Referenced Date':d, LastViewedDate`'Last Viewed Date':d, MasterRecordId`'Master Record ID'[Account]:s, Name`'Account " +
                "Name':s, NumberOfEmployees`Employees:w, OwnerId`'Owner ID'[User]:s, ParentId`'Parent Account ID'[Account]:s, Phone`'Account Phone':s, PhotoUrl`'Photo URL':s, ShippingCity`'Shipping City':s, ShippingCountry`'Shipping " +
                "Country':s, ShippingGeocodeAccuracy`'Shipping Geocode Accuracy':s, ShippingLatitude`'Shipping Latitude':w, ShippingLongitude`'Shipping Longitude':w, ShippingPostalCode`'Shipping Zip/Postal Code':s, ShippingState`'Shipping " +
                "State/Province':s, ShippingStreet`'Shipping Street':s, SicDesc`'SIC Description':s, SystemModstamp`'System Modstamp':d, Type`'Account Type':s, Website:s]", sfTable.ToStringWithDisplayNames());

            Assert.NotNull(sfTable.Relationships);
            Assert.NotNull(sfTable.Relationships.FieldsWithRelationship);
            Assert.Equal(5, sfTable.Relationships.FieldsWithRelationship.Count);

            Assert.Equal("MasterRecordId", sfTable.Relationships.FieldsWithRelationship[0].FieldName);
            Assert.Equal("MasterRecord", sfTable.Relationships.FieldsWithRelationship[0].RelationshipName);
            Assert.Equal("Account", sfTable.Relationships.FieldsWithRelationship[0].TableName);
            
            Assert.Equal("ParentId", sfTable.Relationships.FieldsWithRelationship[1].FieldName);
            Assert.Equal("Parent", sfTable.Relationships.FieldsWithRelationship[1].RelationshipName);
            Assert.Equal("Account", sfTable.Relationships.FieldsWithRelationship[1].TableName);
            
            Assert.Equal("OwnerId", sfTable.Relationships.FieldsWithRelationship[2].FieldName);
            Assert.Equal("Owner", sfTable.Relationships.FieldsWithRelationship[2].RelationshipName);
            Assert.Equal("User", sfTable.Relationships.FieldsWithRelationship[2].TableName);
            
            Assert.Equal("CreatedById", sfTable.Relationships.FieldsWithRelationship[3].FieldName);
            Assert.Equal("CreatedBy", sfTable.Relationships.FieldsWithRelationship[3].RelationshipName);
            Assert.Equal("User", sfTable.Relationships.FieldsWithRelationship[3].TableName);
            
            Assert.Equal("LastModifiedById", sfTable.Relationships.FieldsWithRelationship[4].FieldName);
            Assert.Equal("LastModifiedBy", sfTable.Relationships.FieldsWithRelationship[4].RelationshipName);
            Assert.Equal("User", sfTable.Relationships.FieldsWithRelationship[4].TableName);

            Assert.NotNull(sfTable.Relationships.ReferencedEntities);
            Assert.Equal(49, sfTable.Relationships.ReferencedEntities.Count);
            
            Assert.Equal("ParentId", sfTable.Relationships.ReferencedEntities[0].FieldName);
            Assert.Equal("ChildAccounts", sfTable.Relationships.ReferencedEntities[0].RelationshipName);
            Assert.Equal("Account", sfTable.Relationships.ReferencedEntities[0].TableName);
            
            Assert.Equal("AccountId", sfTable.Relationships.ReferencedEntities[1].FieldName);
            Assert.Equal("AccountContactRelations", sfTable.Relationships.ReferencedEntities[1].RelationshipName);
            Assert.Equal("AccountContactRelation", sfTable.Relationships.ReferencedEntities[1].TableName);
            
            Assert.Equal("AccountId", sfTable.Relationships.ReferencedEntities[2].FieldName);
            Assert.Equal("AccountContactRoles", sfTable.Relationships.ReferencedEntities[2].RelationshipName);
            Assert.Equal("AccountContactRole", sfTable.Relationships.ReferencedEntities[2].TableName);
            
            Assert.Equal("ParentId", sfTable.Relationships.ReferencedEntities[3].FieldName);
            Assert.Equal("Feeds", sfTable.Relationships.ReferencedEntities[3].RelationshipName);
            Assert.Equal("AccountFeed", sfTable.Relationships.ReferencedEntities[3].TableName);

            HashSet<IExternalTabularDataSource> ads = sfTable.Type._type.AssociatedDataSources;
            Assert.NotNull(ads);

            // Tests skipped as ConnectorType.AddDataSource is skipping the creation of AssociatedDataSources
#if false
            Assert.Single(ads);

            TabularDataSource tds = Assert.IsType<TabularDataSource>(ads.First());
            Assert.NotNull(tds);
            Assert.NotNull(tds.DataEntityMetadataProvider);

            CdpEntityMetadataProvider cemp = Assert.IsType<CdpEntityMetadataProvider>(tds.DataEntityMetadataProvider);
            Assert.True(cemp.TryGetEntityMetadata("Account", out IDataEntityMetadata dem));

            TabularDataSourceMetadata tdsm = Assert.IsType<TabularDataSourceMetadata>(dem);
            Assert.Equal("default", tdsm.DatasetName);
            Assert.Equal("Account", tdsm.EntityName);

            Assert.Equal("Account", tds.EntityName.Value);
            Assert.True(tds.IsDelegatable);
            Assert.True(tds.IsPageable);
            Assert.True(tds.IsRefreshable);
            Assert.True(tds.IsSelectable);
            Assert.True(tds.IsWritable);
            Assert.Equal(DataSourceKind.Connected, tds.Kind);
            Assert.Equal("Account", tds.Name);
            Assert.True(tds.RequiresAsync);
            Assert.NotNull(tds.ServiceCapabilities);
#endif

            Assert.NotNull(sfTable._connectorType);
            Assert.Null(sfTable._connectorType.Relationships);
            
#pragma warning disable CS0618 // Type or member is obsolete

            // Enable IR rewritter to auto-inject ServiceProvider where needed
            engine.EnableTabularConnectors();

#pragma warning restore CS0618 // Type or member is obsolete

            SymbolValues symbolValues = new SymbolValues().Add("Accounts", sfTable);
            RuntimeConfig rc = new RuntimeConfig(symbolValues)
                                    .AddService<ConnectorLogger>(logger)
                                    .AddService<HttpClient>(client);

            // Expression with tabular connector
            string expr = @"First(Accounts).'Account ID'";
            CheckResult check = engine.Check(expr, options: new ParserOptions() { AllowsSideEffects = true }, symbolTable: symbolValues.SymbolTable);
            Assert.True(check.IsSuccess);

            // Confirm that InjectServiceProviderFunction has properly been added
            string ir = new Regex("RuntimeValues_[0-9]+").Replace(check.PrintIR(), "RuntimeValues_XXX");
            Assert.Equal(
                "FieldAccess(First:![AccountSource:s, BillingCity:s, BillingCountry:s, BillingGeocodeAccuracy:s, BillingLatitude:w, BillingLongitude:w, BillingPostalCode:s, BillingState:s, BillingStreet:s, CreatedById:s, " +
                "CreatedDate:d, Description:s, Id:s, Industry:s, IsDeleted:b, Jigsaw:s, JigsawCompanyId:s, LastActivityDate:D, LastModifiedById:s, LastModifiedDate:d, LastReferencedDate:d, LastViewedDate:d, MasterRecordId:s, " +
                "Name:s, NumberOfEmployees:w, OwnerId:s, ParentId:s, Phone:s, PhotoUrl:s, ShippingCity:s, ShippingCountry:s, ShippingGeocodeAccuracy:s, ShippingLatitude:w, ShippingLongitude:w, ShippingPostalCode:s, ShippingState:s, " +
                "ShippingStreet:s, SicDesc:s, SystemModstamp:d, Type:s, Website:s](InjectServiceProviderFunction:*[AccountSource:s, BillingCity:s, BillingCountry:s, BillingGeocodeAccuracy:s, BillingLatitude:w, BillingLongitude:w, " +
                "BillingPostalCode:s, BillingState:s, BillingStreet:s, CreatedById:s, CreatedDate:d, Description:s, Id:s, Industry:s, IsDeleted:b, Jigsaw:s, JigsawCompanyId:s, LastActivityDate:D, LastModifiedById:s, LastModifiedDate:d, " +
                "LastReferencedDate:d, LastViewedDate:d, MasterRecordId:s, Name:s, NumberOfEmployees:w, OwnerId:s, ParentId:s, Phone:s, PhotoUrl:s, ShippingCity:s, ShippingCountry:s, ShippingGeocodeAccuracy:s, ShippingLatitude:w, " +
                "ShippingLongitude:w, ShippingPostalCode:s, ShippingState:s, ShippingStreet:s, SicDesc:s, SystemModstamp:d, Type:s, Website:s](ResolvedObject('Accounts:RuntimeValues_XXX'))), Id)", ir);

            // Use tabular connector. Internally we'll call ConnectorTableValueWithServiceProvider.GetRowsInternal to get the data
            testConnector.SetResponseFromFile(@"Responses\SF GetData.json");
            FormulaValue result = await check.GetEvaluator().EvalAsync(CancellationToken.None, rc);

            StringValue accountId = Assert.IsType<StringValue>(result);
            Assert.Equal("001DR00001Xj1YmYAJ", accountId.Value);
        }

        [Fact]
        public async Task SF_CdpTabular()
        {
            using var testConnector = new LoggingTestServer(null /* no swagger */, _output);
            var config = new PowerFxConfig(Features.PowerFxV1);
            var engine = new RecalcEngine(config);

            ConsoleLogger logger = new ConsoleLogger(_output);
            using var httpClient = new HttpClient(testConnector);
            string connectionId = "ec5fe6d1cad744a0a716fe4597a74b2e";
            string jwt = "eyJ0eXAiOiJ...";
            using var client = new PowerPlatformConnectorClient("tip2-001.azure-apihub.net", "53d7f409-4bce-e458-8245-5fa1346ec433", connectionId, () => jwt, httpClient)
            {
                SessionId = "8e67ebdc-d402-455a-b33a-304820832384"
            };

            ConnectorTable tabularService = new ConnectorTable("default", "Account");

            Assert.False(tabularService.IsInitialized);
            Assert.Equal("Account", tabularService.TableName);

            testConnector.SetResponseFromFiles(@"Responses\SF GetDatasetsMetadata.json", @"Responses\SF GetSchema.json");
            await tabularService.InitAsync(client, $"/apim/salesforce/{connectionId}", CancellationToken.None, logger);
            Assert.True(tabularService.IsInitialized);

            ConnectorTableValue sfTable = tabularService.GetTableValue();
            Assert.True(sfTable._tabularService.IsInitialized);
            Assert.True(sfTable.IsDelegable);

            Assert.Equal(
                "*[AccountSource`'Account Source':s, BillingCity`'Billing City':s, BillingCountry`'Billing Country':s, BillingGeocodeAccuracy`'Billing Geocode Accuracy':s, BillingLatitude`'Billing Latitude':w, BillingLongitude`'Billing " +
                "Longitude':w, BillingPostalCode`'Billing Zip/Postal Code':s, BillingState`'Billing State/Province':s, BillingStreet`'Billing Street':s, CreatedById`'Created By ID':s, CreatedDate`'Created Date':d, Description`'Account " +
                "Description':s, Id`'Account ID':s, Industry:s, IsDeleted`Deleted:b, Jigsaw`'Data.com Key':s, JigsawCompanyId`'Jigsaw Company ID':s, LastActivityDate`'Last Activity':D, LastModifiedById`'Last Modified By " +
                "ID':s, LastModifiedDate`'Last Modified Date':d, LastReferencedDate`'Last Referenced Date':d, LastViewedDate`'Last Viewed Date':d, MasterRecordId`'Master Record ID':s, Name`'Account Name':s, NumberOfEmployees`Employees:w, " +
                "OwnerId`'Owner ID':s, ParentId`'Parent Account ID':s, Phone`'Account Phone':s, PhotoUrl`'Photo URL':s, ShippingCity`'Shipping City':s, ShippingCountry`'Shipping Country':s, ShippingGeocodeAccuracy`'Shipping " +
                "Geocode Accuracy':s, ShippingLatitude`'Shipping Latitude':w, ShippingLongitude`'Shipping Longitude':w, ShippingPostalCode`'Shipping Zip/Postal Code':s, ShippingState`'Shipping State/Province':s, ShippingStreet`'Shipping " +
                "Street':s, SicDesc`'SIC Description':s, SystemModstamp`'System Modstamp':d, Type`'Account Type':s, Website:s]", sfTable.Type.ToStringWithDisplayNames());

#pragma warning disable CS0618 // Type or member is obsolete

            // Enable IR rewritter to auto-inject ServiceProvider where needed
            engine.EnableTabularConnectors();

#pragma warning restore CS0618 // Type or member is obsolete

            SymbolValues symbolValues = new SymbolValues().Add("Accounts", sfTable);
            RuntimeConfig rc = new RuntimeConfig(symbolValues)
                                    .AddService<ConnectorLogger>(logger)
                                    .AddService<HttpClient>(client);

            // Expression with tabular connector
            string expr = @"First(Accounts).'Account ID'";
            CheckResult check = engine.Check(expr, options: new ParserOptions() { AllowsSideEffects = true }, symbolTable: symbolValues.SymbolTable);
            Assert.True(check.IsSuccess);

            // Confirm that InjectServiceProviderFunction has properly been added
            string ir = new Regex("RuntimeValues_[0-9]+").Replace(check.PrintIR(), "RuntimeValues_XXX");
            Assert.Equal(
                "FieldAccess(First:![AccountSource:s, BillingCity:s, BillingCountry:s, BillingGeocodeAccuracy:s, BillingLatitude:w, BillingLongitude:w, BillingPostalCode:s, BillingState:s, BillingStreet:s, CreatedById:s, " +
                "CreatedDate:d, Description:s, Id:s, Industry:s, IsDeleted:b, Jigsaw:s, JigsawCompanyId:s, LastActivityDate:D, LastModifiedById:s, LastModifiedDate:d, LastReferencedDate:d, LastViewedDate:d, MasterRecordId:s, " +
                "Name:s, NumberOfEmployees:w, OwnerId:s, ParentId:s, Phone:s, PhotoUrl:s, ShippingCity:s, ShippingCountry:s, ShippingGeocodeAccuracy:s, ShippingLatitude:w, ShippingLongitude:w, ShippingPostalCode:s, ShippingState:s, " +
                "ShippingStreet:s, SicDesc:s, SystemModstamp:d, Type:s, Website:s](InjectServiceProviderFunction:*[AccountSource:s, BillingCity:s, BillingCountry:s, BillingGeocodeAccuracy:s, BillingLatitude:w, BillingLongitude:w, " +
                "BillingPostalCode:s, BillingState:s, BillingStreet:s, CreatedById:s, CreatedDate:d, Description:s, Id:s, Industry:s, IsDeleted:b, Jigsaw:s, JigsawCompanyId:s, LastActivityDate:D, LastModifiedById:s, LastModifiedDate:d, " +
                "LastReferencedDate:d, LastViewedDate:d, MasterRecordId:s, Name:s, NumberOfEmployees:w, OwnerId:s, ParentId:s, Phone:s, PhotoUrl:s, ShippingCity:s, ShippingCountry:s, ShippingGeocodeAccuracy:s, ShippingLatitude:w, " +
                "ShippingLongitude:w, ShippingPostalCode:s, ShippingState:s, ShippingStreet:s, SicDesc:s, SystemModstamp:d, Type:s, Website:s](ResolvedObject('Accounts:RuntimeValues_XXX'))), Id)", ir);

            // Use tabular connector. Internally we'll call ConnectorTableValueWithServiceProvider.GetRowsInternal to get the data
            testConnector.SetResponseFromFile(@"Responses\SF GetData.json");
            FormulaValue result = await check.GetEvaluator().EvalAsync(CancellationToken.None, rc);

            StringValue accountId = Assert.IsType<StringValue>(result);
            Assert.Equal("001DR00001Xj1YmYAJ", accountId.Value);
        }
    }

    public static class Exts2
    {
        public static string ToStringWithDisplayNames(this ConnectorTableValue ctv)
        {
            string str = ctv.Type.ToStringWithDisplayNames();
            foreach (ConnectorType field in ctv._connectorType.Fields.Where(ft => ft.ExternalTables != null && ft.ExternalTables.Any()))
            {
                string fn = field.Name;
                if (!string.IsNullOrEmpty(field.DisplayName))
                {
                    string dn = TexlLexer.EscapeName(field.DisplayName);
                    fn = $"{fn}`{dn}";
                }

                string fn2 = $"{fn}[{string.Join(",", field.ExternalTables)}]";
                str = str.Replace(fn, fn2);
            }

            return str;
        }
    }
}
