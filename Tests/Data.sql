-- Table [dbo].[CharsAndBinaries]
BEGIN TRANSACTION;
INSERT INTO [dbo].[CharsAndBinaries] ([Binary], [Char], [Image], [NText], [NVarChar], [Text], [UniqueIdentifier], [VarChar], [Xml]) VALUES
  (0x01020000000000000000, 'aa        ', 0x0102, N'Привет', N'Привет', 'Hello', '01234567-89ab-cdef-0123-456789abcdef', 'Hello', N'<a><b value="привет" /></a>');
COMMIT;

-- Table [dbo].[DateTimes]
BEGIN TRANSACTION;
INSERT INTO [dbo].[DateTimes] ([Date], [DateTime], [DateTime2_0], [DateTime2_7], [DatetimeOffset_0], [DatetimeOffset_7], [SmallDateTime], [Time_0], [Time_7]) VALUES
  ('2015-02-15', '2015-02-15T00:00:00', '2015-02-15T00:00:00', '2015-02-15T00:00:00.1234567', '2015-02-15T00:00:00+08:00', '2015-02-15T00:00:00.1234567+08:00', '2015-02-15T00:00:00', '00:00:01', '23:59:59.9999999');
COMMIT;

-- Table [dbo].[Numbers]
BEGIN TRANSACTION;
INSERT INTO [dbo].[Numbers] ([BigInt], [Bit], [Decimal_38_4], [Float], [Int], [Money], [Numeric_38_4], [Real], [SmallInt], [SmallMoney], [TinyInt]) VALUES
  (9223372036854775807, 1, 123456789012345678901234567890.1234, 9.22337203685478E+18, 2147483647, 922337203685477.5807, 123456789012345678901234567890.1234, 3.402823E+38, 32767, 214748.3647, 255);
COMMIT;

-- Table [dbo].[Specials]
BEGIN TRANSACTION;
INSERT INTO [dbo].[Specials] ([Geography], [Geometry], [HierarchyId]) VALUES
  ('POINT (1 2 3 3.5)', 'POINT (1 2 3 3.5)', '/'),
  ('POINT (1 2 3 3.5)', 'POINT (1 2 3 3.5)', '/1/2/3/');
COMMIT;