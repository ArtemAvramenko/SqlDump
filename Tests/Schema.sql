CREATE TABLE [dbo].[CharsAndBinaries](
	[Binary] [binary](10) NULL,
	[Char] [char](10) NULL,
	[Image] [image] NULL,
	[NText] [ntext] NULL,
	[NVarChar] [nvarchar](max) NULL,
	[Text] [text] NULL,
	[UniqueIdentifier] [uniqueidentifier] NULL,
	[VarChar] [varchar](max) NULL,
	[Xml] [xml] NULL
)
GO

CREATE TABLE [dbo].[DateTimes](
	[DateTime] [datetime] NULL,
	[DateTime2_0] [datetime2](0) NULL,
	[DateTime2_7] [datetime2](7) NULL,
	[SmallDateTime] [smalldatetime] NULL,
	[Time_0] [time](0) NULL,
	[Time_7] [time](7) NULL,
	[DatetimeOffset_0] [datetimeoffset](0) NULL,
	[DatetimeOffset_7] [datetimeoffset](7) NULL,
	[Date] [date] NULL
)
GO

CREATE TABLE [dbo].[Numbers](
	[BigInt] [bigint] NULL,
	[Bit] [bit] NULL,
	[Decimal_38_4] [decimal](38, 4) NULL,
	[Float] [float] NULL,
	[Int] [int] NOT NULL,
	[Money] [money] NULL,
	[Numeric_38_4] [numeric](38, 4) NULL,
	[Real] [real] NULL,
	[SmallInt] [smallint] NULL,
	[SmallMoney] [smallmoney] NULL,
	[TinyInt] [tinyint] NULL,
)
GO

CREATE TABLE [dbo].[Specials](
	[HierarchyId] [hierarchyid] NULL,
	[Geography] [geography] NULL,
	[Geometry] [geometry] NULL
)
GO
