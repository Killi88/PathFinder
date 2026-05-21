<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:msxsl="urn:schemas-microsoft-com:xslt"
                exclude-result-prefixes="msxsl s0 CodeMapper DateMapper StringMapper ContextAccessor DataModelAccessor OCMHelper StringHelper ListHelper ListHelperClear XMLHelper MultipleRFFListHelper SubscriptionHelper userCSharp StringWrapper ICS2Helper" version="1.0"
                xmlns:s0="http://www.cargowise.com/Schemas/Universal/2011/11"
                xmlns:ns0="http://schemas.microsoft.com/BizTalk/EDI/EDIFACT/2006"
                xmlns:CodeMapper="http://schemas.microsoft.com/BizTalk/2003/CodeMapper"
                xmlns:DateMapper="http://schemas.microsoft.com/BizTalk/2003/DateMapper"
                xmlns:StringMapper="http://schemas.microsoft.com/BizTalk/2003/StringMapper"
                xmlns:ContextAccessor="http://schemas.microsoft.com/BizTalk/2003/ContextAccessor"
                xmlns:DataModelAccessor="http://schemas.microsoft.com/BizTalk/2003/DataModelAccessor"
                xmlns:OCMHelper="http://schemas.microsoft.com/BizTalk/2003/OCMHelper"
                xmlns:StringHelper="http://schemas.microsoft.com/BizTalk/2003/StringHelper"
                xmlns:ListHelper="http://schemas.microsoft.com/BizTalk/2003/ListHelper"
                xmlns:ListHelperClear="http://schemas.microsoft.com/BizTalk/2003/ListHelperClear"
                xmlns:XMLHelper="http://schemas.microsoft.com/BizTalk/2003/XMLHelper"
                xmlns:MultipleRFFListHelper="http://schemas.microsoft.com/BizTalk/2003/MultipleRFFListHelper"
                xmlns:SubscriptionHelper="http://schemas.microsoft.com/BizTalk/2003/SubscriptionHelper"
                xmlns:userCSharp="http://schemas.microsoft.com/BizTalk/2003/userCSharp"
                xmlns:StringWrapper="http://schemas.microsoft.com/BizTalk/2003/StringWrapper"
                xmlns:ICS2Helper="http://schemas.microsoft.com/BizTalk/2003/ICS2Helper">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="yes"/>

  <xsl:template match="/">
    <xsl:apply-templates select="s0:UniversalShipment/s0:Shipment"/>
  </xsl:template>

  <xsl:variable name="MappingId" select="'OCMIFTMIN'"/>
  <xsl:variable name="MappingDescription" select="'OCM IFTMIN Configuration'"/>
  <xsl:variable name="SourceParty" select="ContextAccessor:GetContextProperty('SourceParty', 'http://schemas.microsoft.com/BizTalk/2003/system-properties')"/>
  <xsl:variable name="overrideEmailSubject" select="ContextAccessor:GetContextProperty('OverrideEmailSubject', 'http://cargowise.com/ehub/processing/2010/06')"/>

  <xsl:variable name="SenderID">
    <xsl:choose>
      <xsl:when test="$SourceParty='OCM_BookingEngine'">
        <xsl:value-of select="$overrideEmailSubject"/>
      </xsl:when>
      <xsl:otherwise>
        <xsl:value-of select="$SourceParty"/>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:variable>

  <xsl:variable name="RecipientID" select="ContextAccessor:GetContextProperty('DestinationParty', 'http://schemas.microsoft.com/BizTalk/2003/system-properties')"/>

  <xsl:variable name="ServiceProvider" select="OCMHelper:GetServiceProvider($RecipientID)" />

  <xsl:variable name="CarrierName" select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION', 'SHIPPING_INSTRUCTION', 'OCM System Configuration', 'CarrierSettings', 'CarrierName', $ServiceProvider)" />
  <xsl:variable name="CarrierID" select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION', 'SHIPPING_INSTRUCTION', 'OCM System Configuration', 'CarrierSettings', 'ID', $ServiceProvider)" />
  <xsl:variable name="CarrierMSGID" select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION', 'SHIPPING_INSTRUCTION', 'OCM System Configuration', 'CarrierSettings', 'MSGID', $ServiceProvider)" />
  <xsl:variable name="CarrierSubscriptionPrefix" select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION', 'SHIPPING_INSTRUCTION', 'OCM System Configuration', 'CarrierSettings', 'SubscriptionPrefix', $ServiceProvider)" />
  <xsl:variable name="combineRecipientMappingNamev1" select="concat($CarrierName, ' Provider Configuration')"/>
  <xsl:variable name="combineRecipientCodeField" select="concat($CarrierName, ' Code')"/>
  <xsl:variable name="shipment" select="/s0:UniversalShipment/s0:Shipment"/>
  <xsl:variable name="shipmentType" select="$shipment/s0:ShipmentType/s0:Code/text()"/>
  <xsl:variable name="isCoLoad" select="OCMHelper:IsCoLoad($shipmentType)"/>
  <xsl:variable name="addInfo_IsCoLoad">
    <xsl:variable name="value" select="$shipment/s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='IsCoLoad']/s0:Value/text()"/>
    <xsl:choose>
      <xsl:when test="$value != ''">
        <xsl:value-of select="StringHelper:ToUpper($value)"/>
      </xsl:when>
      <xsl:otherwise>
        <xsl:value-of select="$isCoLoad" />
      </xsl:otherwise>
    </xsl:choose>
  </xsl:variable>
  <xsl:variable name="addressType">
    <xsl:choose>
      <xsl:when test="$isCoLoad = 'TRUE'">
        <xsl:value-of select="'CoLoadWith'"/>
      </xsl:when>
      <xsl:otherwise>
        <xsl:value-of select="'ShippingLineAddress'"/>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:variable>
  <xsl:variable name="OrgSCAC" select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION' , 'SHIPPING_INSTRUCTION' , 'OCM System Configuration' , 'SCAC' , 'Output Code', $RecipientID,
                  $shipment/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()=$addressType]/s0:RegistrationNumberCollection/s0:RegistrationNumber[s0:Type/s0:Code/text()='CCC' and s0:CountryOfIssue/s0:Code/text()='US']/s0:Value/text())"/>
  <xsl:variable name="AddresFormatExclusion" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'Address Format', 'Exclusion', $RecipientID, $OrgSCAC)"/>
  <xsl:variable name="govRefNoOfSegment" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'Gov Reference Format', 'NumberOfSegment', $RecipientID)"/>
  <xsl:variable name="includeRegulatingCountryCode" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'Gov Reference Format', 'Inc.RegulatingCountry', $RecipientID)"/>
  <xsl:variable name="subShipmentCollection" select="$shipment/s0:SubShipmentCollection" />
  <xsl:variable name="subShipmentCount" select="count($subShipmentCollection/s0:SubShipment)"/>

  <xsl:variable name="ICS2FilingTypeRequired" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'ICS2 Requirement', 'Supported', $RecipientID)"/>
  <xsl:variable name="ICS2FilingType">
    <xsl:choose>
      <xsl:when test="StringHelper:ToUpper($ICS2FilingTypeRequired) = 'TRUE'">
        <xsl:call-template name="GetValueOrDefault">
          <xsl:with-param name="value1" select="$shipment/s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='ICS2FilingType']/s0:Value/text()"/>
        </xsl:call-template>
      </xsl:when>
      <xsl:otherwise></xsl:otherwise>
    </xsl:choose>
  </xsl:variable>

  <xsl:variable name="ICS2FillingType_CarrierWithOneSubShipment">
    <xsl:choose>
      <xsl:when test="$ICS2FilingType = 'Carrier' and $subShipmentCount = 1">TRUE</xsl:when>
      <xsl:otherwise>FALSE</xsl:otherwise>
    </xsl:choose>
  </xsl:variable>

  <xsl:variable name="ICS2FillingType_CarrierWithMoreThanOneSubShipment">
    <xsl:choose>
      <xsl:when test="$ICS2FilingType = 'Carrier' and $subShipmentCount &gt; 1">TRUE</xsl:when>
      <xsl:otherwise>FALSE</xsl:otherwise>
    </xsl:choose>
  </xsl:variable>

  <xsl:variable name="groupingMethod" select="StringHelper:Trim($shipment/s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='GroupingMethod']/s0:Value/text())"/>

  <xsl:template match="s0:UniversalShipment/s0:Shipment">

    <xsl:variable name="ClientID" select="DataModelAccessor:GetClientRegistrationCode($SenderID, s0:DataContext/s0:EventBranch/s0:Code/text(), $CarrierName)" />
    <xsl:variable name="InterchangeNum" select="CodeMapper:CallActionProcedureHelper('GetCounterValue','@value','@name',concat('CargoWise.eHub.Products.OceanCarrierMessaging.Transforms.', $CarrierName, '.UNH1'),'@maxlength','14')" />
    <xsl:variable name="dataVersion" select="s0:DataContext/s0:DocumentaryOverride/s0:DataVersion/text()" />
    <xsl:variable name="consolID" select="s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key[1]/text()"/>
    <xsl:variable name="SubscribeClientID" select="DataModelAccessor:InsertSubscriptionValue($CarrierID, $CarrierName, $SenderID, $ClientID)" />
    <xsl:variable name="SubscribeInterchangeNum" select="DataModelAccessor:InsertSubscriptionValue($CarrierMSGID, $CarrierName, $SenderID, $InterchangeNum, $consolID)" />

    <xsl:variable name="OverrideEDIHeader" select="ContextAccessor:SetContextProperty('OverrideEDIHeader', 'http://schemas.microsoft.com/BizTalk/2006/edi-properties', 'true')"/>
    <xsl:variable name="PartySenderID" select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION' , 'SHIPPING_INSTRUCTION' , 'OCM System Configuration' , 'UNB3' , 'PartySenderID', $RecipientID, $OrgSCAC)" />
    <xsl:variable name="DestinationPartySenderIdentifierValue">
      <xsl:choose>
        <xsl:when test="$PartySenderID != ''">
          <xsl:value-of select="$PartySenderID" />
        </xsl:when>
        <xsl:otherwise>CARGOWISE</xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <xsl:variable name="DestinationPartySenderIdentifier" select="ContextAccessor:SetContextProperty('DestinationPartySenderIdentifier' , 'http://schemas.microsoft.com/Edi/PropertySchema' , $DestinationPartySenderIdentifierValue)"/>
    <xsl:variable name="DestinationPartySenderQualifier" select="ContextAccessor:SetContextProperty('DestinationPartySenderQualifier' , 'http://schemas.microsoft.com/Edi/PropertySchema' , 'ZZZ')"/>

    <xsl:variable name="PartyReceiverID" select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION' , 'SHIPPING_INSTRUCTION' , 'OCM System Configuration' , 'UNB3' , 'PartyReceiverID', $RecipientID, $OrgSCAC)" />

    <xsl:variable name="DestinationPartyReceiverIdentifierValue">
      <xsl:choose>
        <xsl:when test="$PartyReceiverID != ''">
          <xsl:value-of select="$PartyReceiverID"/>
        </xsl:when>
        <xsl:otherwise>
          <xsl:value-of select="userCSharp:ThrowPartyReceiverIDNotFound($RecipientID, $OrgSCAC)"/>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <xsl:variable name="DestinationPartyReceiverIdentifier" select="ContextAccessor:SetContextProperty('DestinationPartyReceiverIdentifier' , 'http://schemas.microsoft.com/Edi/PropertySchema' , $DestinationPartyReceiverIdentifierValue)"/>
    <xsl:variable name="DestinationPartyReceiverQualifier" select="ContextAccessor:SetContextProperty('DestinationPartyReceiverQualifier' , 'http://schemas.microsoft.com/Edi/PropertySchema' , 'ZZZ')"/>
    <xsl:variable name="UNB5" select="ContextAccessor:SetContextProperty('UNB5', 'http://schemas.microsoft.com/BizTalk/2006/edi-properties', $InterchangeNum)"/>
    <xsl:variable name="UNH1" select="ContextAccessor:SetContextProperty('UNH1', 'http://schemas.microsoft.com/BizTalk/2006/edi-properties', $InterchangeNum)"/>
    <xsl:variable name="FileName" select="ContextAccessor:SetContextProperty('OverrideFilename', 'http://cargowise.com/ehub/processing/2010/06', concat('IFTMIN_',$ClientID,'_',$InterchangeNum))"/>

    <xsl:variable name="UNB9" select="ContextAccessor:SetContextProperty('UNB9', 'http://schemas.microsoft.com/BizTalk/2006/edi-properties', '')" />
    <xsl:variable name="UNB11" select="ContextAccessor:SetContextProperty('UNB11', 'http://schemas.microsoft.com/BizTalk/2006/edi-properties', '')" />
    <xsl:variable name="EarlyTerminateEdifactUnb" select="ContextAccessor:SetContextProperty('EarlyTerminateEdifactUnb', 'http://cargowise.com/ehub/processing/2010/06', 'true')"/>

    <xsl:variable name="previousConsolReferenceWithJobNo" select="CodeMapper:CallActionProcedureHelper('SelectSubscribedReference', '@reference', '@senderId', $CarrierName, '@recipientId', $SenderID , '@ST_ID', $CarrierMSGID, '@value', $consolID, '@referenceType', 'JobNumber')" />
    <xsl:variable name="previousConsolReference">
      <xsl:choose>
        <xsl:when test="$previousConsolReferenceWithJobNo != ''">
          <xsl:value-of select="$previousConsolReferenceWithJobNo" />
        </xsl:when>
        <xsl:otherwise>
          <xsl:value-of select="CodeMapper:CallActionProcedureHelper('SelectSubscribedReference', '@reference', '@senderId', $CarrierName, '@recipientId', $SenderID , '@ST_ID', $CarrierMSGID, '@value', $consolID)" />
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <xsl:variable name="SubscriberConsolReference">
      <xsl:choose>
        <xsl:when test="$previousConsolReference!=''">
          <xsl:value-of select="$previousConsolReference" />
        </xsl:when>
        <xsl:otherwise>
          <xsl:variable name="msgCounter" select="CodeMapper:CallActionProcedureHelper('GetCounterValue','@value','@name',concat('CargoWise.eHub.Products.OceanCarrierMessaging.Transforms.', $CarrierName, '.BGM'),'@maxlength','14')" />
          <xsl:variable name="formattedCounter" select='format-number($msgCounter, "0000000000")' />
          <xsl:variable name="NewConsolReference" select="concat($CarrierSubscriptionPrefix, $formattedCounter)" />
          <xsl:variable name="SubScriberValue1" select="DataModelAccessor:InsertSubscriptionValue($CarrierMSGID, $CarrierName, $SenderID, $NewConsolReference, $consolID, 'JobNumber')" />
          <xsl:variable name="SubScriberValue2" select="DataModelAccessor:InsertSubscriptionValue($CarrierMSGID, $CarrierName, $SenderID, $consolID, $NewConsolReference, 'JobNumber')" />
          <xsl:value-of select="$NewConsolReference" />
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>

    <xsl:variable name="purposeCode" select="s0:DataContext/s0:DocumentaryOverride/s0:Purpose/s0:Code/text()"/>
    <xsl:variable name="documentName" select="s0:DataContext/s0:DocumentaryOverride/s0:DocumentName/text()"/>
    <xsl:variable name="SubscribeShipmentType" select="DataModelAccessor:InsertSubscriptionValue($CarrierMSGID, $CarrierName, $SenderID, $consolID, $shipmentType, 'ShipmentType')" />
    <xsl:variable name="SubscribeActionPurpose" select="DataModelAccessor:InsertSubscriptionValue($CarrierMSGID, $CarrierName, $SenderID, $consolID, $purposeCode, 'ActionPurpose')" />
    <xsl:variable name="SubscribeDocumentName" select="DataModelAccessor:InsertSubscriptionValue($CarrierMSGID, $CarrierName, $SenderID, $consolID, $documentName, 'DocumentName')" />
    <xsl:variable name="SubscribeForwardingType" select="DataModelAccessor:InsertSubscriptionValue($CarrierMSGID, $CarrierName, $SenderID, $consolID, 'ForwardingConsol', 'ForwardingType')" />
    <xsl:variable name="formVersion" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='FormVersion']/s0:Value/text()" />
    <xsl:if test="$formVersion !=''">
      <xsl:variable name="SubscribeFormVersion" select="DataModelAccessor:InsertSubscriptionValue($CarrierMSGID, $CarrierName, $SenderID, $SubscriberConsolReference, $formVersion, 'FormVersion')" />
    </xsl:if>

    <xsl:variable name="loadPortPrefix" select="substring(s0:PortOfLoading/s0:Code/text(),1,2)"/>
    <xsl:variable name="dischargePortPrefix" select="substring(s0:PortOfDischarge/s0:Code/text(),1,2)"/>
    <xsl:variable name="originPortPrefix" select="substring(s0:PortOfOrigin/s0:Code/text(),1,2)"/>
    <xsl:variable name="portOfDestination" select="s0:PortOfDestination/s0:Code/text()"/>
    <xsl:variable name="destinationPortPrefix" select="substring(s0:PortOfDestination/s0:Code/text(),1,2)"/>

    <xsl:variable name="mainLeg" select="s0:TransportLegCollection/s0:TransportLeg[s0:LegType/text()='Main'][1]"/>
    <xsl:variable name="scac" select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION' , 'SHIPPING_INSTRUCTION' , 'OCM System Configuration' , 'SCAC' , 'Output Code', $RecipientID,
                  $mainLeg/s0:Carrier/s0:RegistrationNumberCollection/s0:RegistrationNumber[s0:Type/s0:Code/text()='CCC' and s0:CountryOfIssue/s0:Code/text()='US']/s0:Value/text())"/>

    <xsl:variable name="unitOfWeight">
      <xsl:choose>
        <xsl:when test="s0:PackingLineCollection/s0:PackingLine[1]/s0:WeightUnit/s0:Code/text()='LB'">LBR</xsl:when>
        <xsl:otherwise>KGM</xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <xsl:variable name="unitOfVolume">
      <xsl:choose>
        <xsl:when test="s0:PackingLineCollection/s0:PackingLine[1]/s0:VolumeUnit/s0:Code/text()='CF'">FTQ</xsl:when>
        <xsl:otherwise>MTQ</xsl:otherwise>
      </xsl:choose>
    </xsl:variable>

    <xsl:choose>
      <xsl:when test="$groupingMethod != ''">
        <xsl:for-each select="s0:PackingLineCollection/s0:PackingLine/s0:PackingLineCollection/s0:PackingLine">
          <xsl:variable name="addPacklineVolume" select="userCSharp:CalculatePacklineVolume(s0:ContainerNumber/text(), s0:Volume/text())"/>
          <xsl:variable name="addPacklineWeight" select="userCSharp:CalculatePacklineWeight(s0:ContainerNumber/text(), s0:Weight/text())"/>
        </xsl:for-each>
      </xsl:when>
      <xsl:otherwise>
        <xsl:for-each select="s0:PackingLineCollection/s0:PackingLine">
          <xsl:variable name="addPacklineVolume" select="userCSharp:CalculatePacklineVolume(s0:ContainerNumber/text(), s0:Volume/text())"/>
          <xsl:variable name="addPacklineWeight" select="userCSharp:CalculatePacklineWeight(s0:ContainerNumber/text(), s0:Weight/text())"/>
        </xsl:for-each>
      </xsl:otherwise>
    </xsl:choose>

    <xsl:variable name="InboxPK" select="ContextAccessor:GetContextProperty('InternalTrackingID', 'http://cargowise.com/ehub/tracking/2010/06')"/>
    <xsl:variable name="SubscribeInboxPK" select="DataModelAccessor:InsertSubscriptionValue($CarrierMSGID, $CarrierName, $SenderID, $InboxPK, $InterchangeNum)" />

    <xsl:variable name="currentUser" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='CurrentUser']"/>
    <xsl:variable name="currentUserAddress" select="concat($currentUser/s0:CompanyName/text(), '+', $currentUser/s0:Postcode/text(), '+', $currentUser/s0:City/text(), '+', $currentUser/s0:Country/s0:Code/text())"/>
    <xsl:variable name="bookingParty" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='BookingPartyDocumentaryAddress']"/>
    <xsl:variable name="bookingPartyAddress" select="concat($bookingParty/s0:CompanyName/text(), '+', $bookingParty/s0:Postcode/text(), '+', $bookingParty/s0:City/text(), '+', $bookingParty/s0:Country/s0:Code/text())"/>
    <xsl:variable name="consignee" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsigneeDocumentaryAddress']" />
    <xsl:variable name="notifyParty" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='NotifyParty']" />
    <xsl:variable name="consignor" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsignorDocumentaryAddress']" />
    <xsl:variable name="consignorAddress" select="concat($consignor/s0:CompanyName/text(), '+', $consignor/s0:Postcode/text(), '+', $consignor/s0:City/text(), '+', $consignor/s0:Country/s0:Code/text())"/>
    <xsl:variable name="nadFWClientID">
      <xsl:choose>
        <xsl:when test="$bookingPartyAddress = $currentUserAddress">
          <xsl:value-of select="$ClientID"/>
        </xsl:when>
        <xsl:otherwise></xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <xsl:variable name="nadCZClientID">
      <xsl:choose>
        <xsl:when test="$consignorAddress = $currentUserAddress">
          <xsl:value-of select="$ClientID"/>
        </xsl:when>
        <xsl:otherwise></xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <xsl:variable name="numberFormat" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'Number Format', 'Format', $RecipientID, $OrgSCAC)"/>

    <xsl:variable name="InsertBoleroSubscription" select="SubscriptionHelper:InsertBoleroSubscription($SenderID, $ClientID, $ServiceProvider, $SubscriberConsolReference, $consolID)" />

    <ns0:EFACT_D99B_IFTMIN>
      <UNH>
        <UNH1>
          <xsl:value-of select="$InterchangeNum"/>
        </UNH1>
        <UNH2>
          <UNH2.1>IFTMIN</UNH2.1>
          <UNH2.2>D</UNH2.2>
          <UNH2.3>99B</UNH2.3>
          <UNH2.4>UN</UNH2.4>
        </UNH2>
      </UNH>
      <ns0:BGM>
        <ns0:C002>
          <C00201>
            <xsl:text>340</xsl:text>
          </C00201>
        </ns0:C002>
        <xsl:variable name="revisionCount" select="s0:DataContext/s0:DocumentaryOverride/s0:DataVersion/text()"/>
        <ns0:C106>
          <C10601>
            <xsl:value-of select="$SubscriberConsolReference"/>
          </C10601>
          <C10603>
            <xsl:value-of select="StringMapper:FormatDecimal($revisionCount,'000000',false())"/>
          </C10603>
        </ns0:C106>
        <BGM03>
          <xsl:choose>
            <xsl:when test="$purposeCode='AMD'">5</xsl:when>
            <xsl:when test="$purposeCode='ORG'">9</xsl:when>
          </xsl:choose>
        </BGM03>
      </ns0:BGM>
      <ns0:DTM>
        <ns0:C507>
          <C50701>137</C50701>
          <C50702>
            <xsl:value-of select="DateMapper:ConvertXmlDateString(s0:DataContext/s0:TriggerDate/text(), 'yyyyMMddHHmm')"/>
          </C50702>
          <C50703>203</C50703>
        </ns0:C507>
      </ns0:DTM>
      <xsl:variable name="deliveryMode" select="(s0:ContainerCollection/s0:Container/s0:DeliveryMode)[1]/text()"/>
      <xsl:variable name="TSRValue" select="OCMHelper:GetTSRValue($deliveryMode)"/>
      <ns0:TSR>
        <ns0:C536>
          <C53601>
            <xsl:value-of select="$TSRValue"/>
          </C53601>
        </ns0:C536>
        <ns0:C233>
          <C23301>
            <xsl:choose>
              <xsl:when test="s0:ContainerMode/s0:Code/text()='LCL'">3</xsl:when>
              <xsl:otherwise>2</xsl:otherwise>
            </xsl:choose>
          </C23301>
        </ns0:C233>
      </ns0:TSR>

      <xsl:variable name="numberFormatMOA44" select="userCSharp:GetNumberFormat($numberFormat, 'MOA44=', 2)" />
      <xsl:variable name="goodsValue" select="StringMapper:FormatDecimal(s0:GoodsValue/text(), $numberFormatMOA44, false())"/>
      <xsl:if test="$goodsValue!='' and number($goodsValue)!=0">
        <ns0:CUX>
          <ns0:C504>
            <C50401>4</C50401>
            <C50402>
              <xsl:value-of select="s0:GoodsValueCurrency/s0:Code/text()"/>
            </C50402>
          </ns0:C504>
        </ns0:CUX>
        <ns0:MOA>
          <ns0:C516>
            <C51601>44</C51601>
            <C51602>
              <xsl:value-of select="$goodsValue"/>
            </C51602>
          </ns0:C516>
        </ns0:MOA>
      </xsl:if>

      <xsl:variable name="resetFTXCounter" select="userCSharp:ResetFTXCounter()" />

      <xsl:for-each select="s0:BillOfLadingClauseCollection/s0:BillOfLadingClause">
        <xsl:call-template name="FTX-BLC-Code">
          <xsl:with-param name="code" select="s0:Type/s0:Code/text()"/>
          <xsl:with-param name="description" select="s0:Type/s0:Description/text()"/>
        </xsl:call-template>
      </xsl:for-each>

      <xsl:call-template name="FTX">
        <xsl:with-param name="type" select="'BLC'"/>
        <xsl:with-param name="text" select="s0:NoteCollection/s0:Note[s0:Description/text()='OtherBillClauses']/s0:NoteText/text()"/>
      </xsl:call-template>
      <xsl:call-template name="FTX">
        <xsl:with-param name="type" select="'AAI'"/>
        <xsl:with-param name="text" select="s0:NoteCollection/s0:Note[s0:Description/text()='Goods Handling Instructions']/s0:NoteText/text()"/>
      </xsl:call-template>
      <xsl:call-template name="FTX">
        <xsl:with-param name="type" select="'AAI'"/>
        <xsl:with-param name="text" select="s0:NoteCollection/s0:Note[s0:Description/text()='Special Instructions']/s0:NoteText/text()"/>
      </xsl:call-template>
      <xsl:call-template name="FTX">
        <xsl:with-param name="type" select="'AAI'"/>
        <xsl:with-param name="text" select="s0:NoteCollection/s0:Note[s0:Description/text()='Forwarding Instruction Notes']/s0:NoteText/text()"/>
      </xsl:call-template>

      <xsl:variable name="eBLDocumentationProvider" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='eBLDocumentationProvider']/s0:Value/text()" />
      <xsl:if test="$eBLDocumentationProvider != ''">
        <xsl:variable name="eBLDocProvider" select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION', 'SHIPPING_INSTRUCTION', 'OCM System Configuration', 'eBL Provider', 'Output Code', $RecipientID, $OrgSCAC, $eBLDocumentationProvider)"/>
        <xsl:call-template name="FTX">
          <xsl:with-param name="type" select="'ACA'"/>
          <xsl:with-param name="code" select="$eBLDocProvider"/>
          <xsl:with-param name="text" select="$eBLDocumentationProvider"/>
        </xsl:call-template>
      </xsl:if>

      <xsl:variable name="resetRFFList" select="MultipleRFFListHelper:Reset()" />
      <xsl:call-template name="AddRFF">
        <xsl:with-param name="org" select="$consignee"/>
        <xsl:with-param name="addressType" select="'CN'" />
        <xsl:with-param name="countryOfIssue" select="$destinationPortPrefix"/>
        <xsl:with-param name="portOfDestination" select="$portOfDestination"/>
      </xsl:call-template>
      <xsl:call-template name="AddRFF">
        <xsl:with-param name="org" select="$notifyParty"/>
        <xsl:with-param name="addressType" select="'NI'" />
        <xsl:with-param name="countryOfIssue" select="$destinationPortPrefix"/>
        <xsl:with-param name="portOfDestination" select="$portOfDestination"/>
      </xsl:call-template>
      <xsl:call-template name="AddRFF">
        <xsl:with-param name="org" select="$consignor"/>
        <xsl:with-param name="addressType" select="'CZ'" />
        <xsl:with-param name="countryOfIssue" select="$originPortPrefix"/>
        <xsl:with-param name="portOfDestination" select="$portOfDestination"/>
      </xsl:call-template>

      <xsl:variable name="calculateRFFList" select="MultipleRFFListHelper:CalculateRFFList($govRefNoOfSegment, $includeRegulatingCountryCode)" />
      <xsl:if test="MultipleRFFListHelper:RFFRestCount() > 0 ">
        <xsl:for-each select="MultipleRFFListHelper:RFFRestCollection()">
          <xsl:call-template name="FTX">
            <xsl:with-param name="type" select="'AAI'"/>
            <xsl:with-param name="text" select="Value/text()"/>
          </xsl:call-template>
        </xsl:for-each>
      </xsl:if>

      <xsl:variable name="referenceNumber">
        <xsl:choose>
          <xsl:when test="$isCoLoad = 'TRUE'">
            <xsl:value-of select="s0:CoLoadBookingConfirmationReference"/>
          </xsl:when>
          <xsl:otherwise>
            <xsl:value-of select="s0:BookingConfirmationReference"/>
          </xsl:otherwise>
        </xsl:choose>
      </xsl:variable>
      <xsl:call-template name="FTX">
        <xsl:with-param name="type" select="'AAI'"/>
        <xsl:with-param name="text">
          <xsl:for-each select="(msxsl:node-set($referenceNumber) | s0:AdditionalReferenceCollection/s0:AdditionalReference[s0:Type/s0:Code/text()='BKG']/s0:ReferenceNumber)/text()[not(text()=preceding::*/text())]">
            <xsl:value-of select="."/>
            <xsl:if test="not(position()=last())">,</xsl:if>
          </xsl:for-each>
        </xsl:with-param>
      </xsl:call-template>

      <xsl:variable name="countBRLeg" select="count(s0:TransportLegCollection/s0:TransportLeg[substring(s0:PortOfDischarge/s0:Code/text(),1,2)='BR' or substring(s0:PortOfLoading/text(),1,2)='BR']) > 0"/>
      <xsl:if test="$countBRLeg='true' or substring(s0:PortOfOrigin/s0:Code/text(),1,2)='BR'
                                      or substring(s0:PortOfDestination/s0:Code/text(),1,2)='BR'
                                      or substring(s0:PlaceOfDelivery/s0:Code/text(),1,2)='BR'
                                      or substring(s0:PlaceOfReceipt/s0:Code/text(),1,2)='BR'">

        <xsl:variable name="woondenPackageValue" select="s0:NoteCollection/s0:Note[s0:Description/text()='WoodenPackageProcessType']/s0:NoteText/text()"/>
        <xsl:call-template name="FTX">
          <xsl:with-param name="type" select="'AAI'"/>
          <xsl:with-param name="text" select="concat('Wooden Package: ', $woondenPackageValue)"/>
        </xsl:call-template>
      </xsl:if>

      <xsl:variable name ="usCanadaManifestSelfFilerIDNode" select="s0:NoteCollection/s0:Note[s0:Description/text()='USCanadaManifestSelfFilerID']"/>
      <xsl:if test="$usCanadaManifestSelfFilerIDNode">
        <xsl:variable name ="usCanadaManifestSelfFilerIDValue" select="$usCanadaManifestSelfFilerIDNode/s0:NoteText/text()"/>
        <xsl:variable name="includeWayBillNumber" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'FTX Format', 'IncludeWayBillNumber', $RecipientID, $OrgSCAC, 'CCI')"/>
        <xsl:variable name="placeOfDeliveryCode">
          <xsl:choose>
            <xsl:when test="s0:PlaceOfDelivery/s0:Code/text() != ''">
              <xsl:value-of select="substring(s0:PlaceOfDelivery/s0:Code/text(), 1, 2)"/>
            </xsl:when>
            <xsl:otherwise>
              <xsl:value-of select="substring(s0:PortOfDischarge/s0:Code/text(), 1, 2)"/>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:variable>

        <xsl:choose>
          <xsl:when test="$usCanadaManifestSelfFilerIDValue!='' and StringHelper:ToUpper($includeWayBillNumber)='TRUE' and count(s0:SubShipmentCollection/s0:SubShipment[s0:WayBillNumber/text()!='']) > 0">
            <xsl:for-each select="s0:SubShipmentCollection/s0:SubShipment[s0:WayBillNumber/text()!='']">
              <xsl:variable name="wayBillNumber" select="s0:WayBillNumber/text()"/>
              <xsl:variable name="shouldCreateFTX-CCI" select="ListHelper:ShouldCreateItem('FTX+CCI+', $wayBillNumber)" />
              <xsl:if test="$shouldCreateFTX-CCI='true'">
                <xsl:call-template name="FTX-CCI-FixedTextValues">
                  <xsl:with-param name="type" select="'CCI'"/>
                  <xsl:with-param name="code" select="'MFS'"/>
                  <xsl:with-param name="text1">
                    <xsl:choose>
                      <xsl:when test="$usCanadaManifestSelfFilerIDValue != ''">1</xsl:when>
                      <xsl:otherwise>5</xsl:otherwise>
                    </xsl:choose>
                  </xsl:with-param>
                  <xsl:with-param name="text2" select="$placeOfDeliveryCode"/>
                  <xsl:with-param name="text3" select="$usCanadaManifestSelfFilerIDValue"/>
                  <xsl:with-param name="text4" select="$wayBillNumber"/>
                </xsl:call-template>
              </xsl:if>
            </xsl:for-each>
          </xsl:when>
          <xsl:otherwise>
            <xsl:variable name="usCCCRegistrationNumber" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ShippingLineAddress']/s0:RegistrationNumberCollection/s0:RegistrationNumber[s0:Type/s0:Code/text()='CCC' and s0:CountryOfIssue/s0:Code/text()='US']/s0:Value/text()"/>
            <xsl:if test="userCSharp:IsContainedSCAC($usCCCRegistrationNumber) or $shipmentType != 'DRT' or $usCanadaManifestSelfFilerIDValue != ''">
              <xsl:call-template name="FTX-CCI-FixedTextValues">
                <xsl:with-param name="type" select="'CCI'"/>
                <xsl:with-param name="code" select="'MFS'"/>
                <xsl:with-param name="text1">
                  <xsl:choose>
                    <xsl:when test="$usCanadaManifestSelfFilerIDValue != ''">1</xsl:when>
                    <xsl:otherwise>5</xsl:otherwise>
                  </xsl:choose>
                </xsl:with-param>
                <xsl:with-param name="text2" select="$placeOfDeliveryCode"/>
                <xsl:with-param name="text3" select="$usCanadaManifestSelfFilerIDValue"/>
              </xsl:call-template>
            </xsl:if>
          </xsl:otherwise>
        </xsl:choose>
      </xsl:if>

      <xsl:if test="$ICS2FilingType != ''">
        <xsl:variable name="clearConsigneeCountriesAndStates" select="ICS2Helper:ClearConsigneeCountriesAndStates()"/>
        <xsl:for-each select="s0:SubShipmentCollection/s0:SubShipment/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text() = 'ConsigneeDocumentaryAddress']">
          <xsl:value-of select="ICS2Helper:AddConsigneeCountryAndState(StringHelper:SubstringSafe(s0:Port/s0:Code/text(), 0, 2), s0:State/text())"/>
        </xsl:for-each>

        <xsl:variable name="clearList" select="ListHelper:ClearList()" />
        <xsl:variable name="isICS2Port">
          <xsl:for-each select="$subShipmentCollection/s0:SubShipment">
            <xsl:variable name="subPortOfDestination" select="s0:PortOfDestination/s0:Code/text()" />
            <xsl:choose>
              <xsl:when test="ICS2Helper:IsICS2Port($subPortOfDestination)">
                <xsl:value-of select="ListHelper:AddToListIfNotExists($subPortOfDestination)"/>
              </xsl:when>
              <xsl:otherwise></xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
        </xsl:variable>

        <xsl:call-template name="FTX-CCI-FixedTextValues">
          <xsl:with-param name="type" select="'CCI'"/>
          <xsl:with-param name="code" select="'ENS'"/>
          <xsl:with-param name="text1">
            <xsl:choose>
              <xsl:when test="$ICS2FilingType = 'Carrier'">5</xsl:when>
              <xsl:when test="$ICS2FilingType = 'Declarant'">1</xsl:when>
              <xsl:otherwise></xsl:otherwise>
            </xsl:choose>
          </xsl:with-param>
          <xsl:with-param name="text2">
            <xsl:choose>
              <xsl:when test="ListHelper:ListCount() > 0">10</xsl:when>
              <xsl:otherwise>11</xsl:otherwise>
            </xsl:choose>
          </xsl:with-param>
        </xsl:call-template>
      </xsl:if>

      <xsl:variable name ="MPCIPartyID">
        <xsl:call-template name="GetValueOrDefault">
          <xsl:with-param name="value1" select="$shipment/s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='MPCIPartyID']/s0:Value/text()"/>
        </xsl:call-template>
      </xsl:variable>

      <xsl:if test="($ICS2FilingType!= '' and $shipmentType != 'DRT') or $MPCIPartyID != ''">

        <xsl:variable name="text1">
          <xsl:choose>
            <xsl:when test="$ICS2FillingType_CarrierWithOneSubShipment='TRUE'">
              <xsl:choose>
                <xsl:when test="$groupingMethod=''">NP</xsl:when>
                <xsl:otherwise>
                  <xsl:call-template name="GetValueOrDefault">
                    <xsl:with-param name="value1" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:PaymentHandlingInstructionCollection/s0:PaymentHandlingInstruction[s0:Category/s0:Code='HPT']/s0:PaymentMethod/s0:Code/text()"/>
                    <xsl:with-param name="value2" select="'D'"/>
                  </xsl:call-template>
                </xsl:otherwise>
              </xsl:choose>
            </xsl:when>
            <xsl:otherwise></xsl:otherwise>
          </xsl:choose>
        </xsl:variable>
        <xsl:variable name="text2">
          <xsl:choose>
            <xsl:when test="$ICS2FillingType_CarrierWithOneSubShipment='TRUE'">
              <xsl:value-of select="translate(s0:SubShipmentCollection/s0:SubShipment[1]/s0:AddInfoCollection/s0:AddInfo[s0:Key/text() = 'CountriesOfRouting']/s0:Value/text(), '|', '-')"/>
            </xsl:when>
            <xsl:otherwise></xsl:otherwise>
          </xsl:choose>
        </xsl:variable>
        <xsl:variable name="text3">
          <xsl:variable name="consigneeCompanyName" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text() = 'ConsigneeDocumentaryAddress']/s0:CompanyName/text()"/>
          <xsl:choose>
            <xsl:when test="$ICS2FillingType_CarrierWithOneSubShipment='TRUE' and (not($consigneeCompanyName) or $consigneeCompanyName = '' or userCSharp:IsToOrder($consigneeCompanyName))">TOS</xsl:when>
            <xsl:otherwise></xsl:otherwise>
          </xsl:choose>
        </xsl:variable>
        <xsl:call-template name="FTX-CCI-FixedTextValues">
          <xsl:with-param name="type" select="'CCI'"/>
          <xsl:with-param name="code" select="'HBL'"/>
          <xsl:with-param name="text1" select="$text1"/>
          <xsl:with-param name="text2" select="$text2"/>
          <xsl:with-param name="text3" select="$text3"/>
        </xsl:call-template>
      </xsl:if>
      <xsl:if test="$MPCIPartyID!= ''">
        <xsl:call-template name="FTX-CCI-FixedTextValues">
          <xsl:with-param name="type" select="'CCI'"/>
          <xsl:with-param name="code" select="'MPCI'"/>
          <xsl:with-param name="text1" select="1"/>
          <xsl:with-param name="text2" select="substring(substring-before($MPCIPartyID, ':'), 1, 2)"/>
          <xsl:with-param name="text3" select="substring-after($MPCIPartyID, ':')"/>
        </xsl:call-template>
      </xsl:if>
      <xsl:if test="$shipmentType != 'DRT' and $ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
        <xsl:call-template name="FTX">
          <xsl:with-param name="type" select="'DOC'"/>
          <xsl:with-param name="code" select="'SMI'"/>
          <xsl:with-param name="text" select="''"/>
          <xsl:with-param name="allowEmptySegment" select="'TRUE'"/>
        </xsl:call-template>
      </xsl:if>

      <xsl:variable name="numberFormatCNT7" select="userCSharp:GetNumberFormat($numberFormat,'CNT7=',3)" />
      <xsl:call-template name="CNT">
        <xsl:with-param name="code" select="'7'"/>
        <xsl:with-param name="value" select="StringMapper:FormatDecimal(sum(s0:PackingLineCollection/s0:PackingLine/s0:Weight/text()),$numberFormatCNT7,false())"/>
        <xsl:with-param name="measurementUnit" select="$unitOfWeight"/>
      </xsl:call-template>
      <xsl:call-template name="CNT">
        <xsl:with-param name="code" select="'11'"/>
        <xsl:with-param name="value" select="sum(s0:PackingLineCollection/s0:PackingLine/s0:PackQty/text())"/>
      </xsl:call-template>
      <xsl:variable name="numberFormatCNT15" select="userCSharp:GetNumberFormat($numberFormat,'CNT15=',4)" />
      <xsl:call-template name="CNT">
        <xsl:with-param name="code" select="'15'"/>
        <xsl:with-param name="value" select="StringMapper:FormatDecimal(sum(s0:PackingLineCollection/s0:PackingLine/s0:Volume/text()),$numberFormatCNT15,false())"/>
        <xsl:with-param name="measurementUnit" select="$unitOfVolume"/>
      </xsl:call-template>
      <xsl:call-template name="CNT">
        <xsl:with-param name="code" select="'16'"/>
        <xsl:with-param name="value" select="count(s0:ContainerCollection/s0:Container)"/>
      </xsl:call-template>

      <xsl:variable name="FreightPayableAt_Code" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='FreightPayableAt_Code']/s0:Value/text()"/>
      <xsl:choose>
        <xsl:when test="$FreightPayableAt_Code != ''">
          <xsl:call-template name="LOC1">
            <xsl:with-param name="code" select="'57'"/>
            <xsl:with-param name="unloco" select="$FreightPayableAt_Code"/>
            <xsl:with-param name="name" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='FreightPayableAt_Name']/s0:Value/text()"/>
          </xsl:call-template>
        </xsl:when>
        <xsl:otherwise>
          <xsl:variable name="locformat" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'Location Format', 'Format', $RecipientID, $OrgSCAC)"/>
          <xsl:variable name="paymentMethod" select="s0:PaymentMethod/s0:Code/text()" />
          <xsl:choose>
            <xsl:when test="$paymentMethod='PPD'">
              <xsl:choose>
                <xsl:when test="$locformat='LOC57=Default'">
                  <xsl:call-template name="LOC1">
                    <xsl:with-param name="code" select="'57'"/>
                    <xsl:with-param name="name" select="'ORIGIN'"/>
                  </xsl:call-template>
                </xsl:when>
                <xsl:otherwise>
                  <xsl:call-template name="LOC1">
                    <xsl:with-param name="code" select="'57'"/>
                    <xsl:with-param name="unloco" select="s0:PlaceOfReceipt/s0:Code/text()"/>
                    <xsl:with-param name="name" select="s0:PlaceOfReceipt/s0:Name/text()"/>
                  </xsl:call-template>
                </xsl:otherwise>
              </xsl:choose>
            </xsl:when>
            <xsl:otherwise>
              <xsl:variable name="payableElseWhereDescription">
                <xsl:choose>
                  <xsl:when test="$paymentMethod='ELS'">
                    <xsl:value-of select="OCMHelper:GetPayableElseWhereDescription($ServiceProvider)" />
                  </xsl:when>
                  <xsl:otherwise></xsl:otherwise>
                </xsl:choose>
              </xsl:variable>

              <xsl:choose>
                <xsl:when test="$payableElseWhereDescription!=''">
                  <xsl:call-template name="LOC1">
                    <xsl:with-param name="code" select="'57'"/>
                    <xsl:with-param name="name" select="$payableElseWhereDescription"/>
                  </xsl:call-template>
                </xsl:when>
                <xsl:otherwise>
                  <xsl:choose>
                    <xsl:when test="$locformat='LOC57=Default'">
                      <xsl:call-template name="LOC1">
                        <xsl:with-param name="code" select="'57'"/>
                        <xsl:with-param name="name" select="'DESTINATION'"/>
                      </xsl:call-template>
                    </xsl:when>
                    <xsl:otherwise>
                      <xsl:call-template name="LOC1">
                        <xsl:with-param name="code" select="'57'"/>
                        <xsl:with-param name="unloco" select="s0:PlaceOfDelivery/s0:Code/text()"/>
                        <xsl:with-param name="name" select="s0:PlaceOfDelivery/s0:Name/text()"/>
                      </xsl:call-template>
                    </xsl:otherwise>
                  </xsl:choose>
                </xsl:otherwise>
              </xsl:choose>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:otherwise>
      </xsl:choose>

      <xsl:variable name="shipperPort" select="s0:PlaceOfIssue"/>
      <xsl:call-template name="LOC1">
        <xsl:with-param name="code" select="'73'"/>
        <xsl:with-param name="unloco" select="$shipperPort/s0:Code/text()"/>
        <xsl:with-param name="name" select="$shipperPort/s0:Name/text()"/>
        <xsl:with-param name="date" select="s0:DateCollection/s0:Date[s0:Type='BillIssued']/s0:Value/text()"/>
        <xsl:with-param name="dtmFormatCode" select="'102'"/>
      </xsl:call-template>

      <xsl:if test="$referenceNumber!=''">
        <xsl:call-template name="RFF">
          <xsl:with-param name="code" select="'BN'"/>
          <xsl:with-param name="ref" select="$referenceNumber"/>
        </xsl:call-template>
      </xsl:if>
      <xsl:for-each select="(s0:AdditionalReferenceCollection/s0:AdditionalReference[s0:Type/s0:Code/text()='BKG']/s0:ReferenceNumber)/text()[not(text()=preceding::*/text())]">
        <xsl:call-template name="RFF">
          <xsl:with-param name="code" select="'BN'"/>
          <xsl:with-param name="ref" select="."/>
        </xsl:call-template>
      </xsl:for-each>
      <xsl:variable name="billNumber">
        <xsl:choose>
          <xsl:when test="$isCoLoad = 'TRUE'">
            <xsl:value-of select="s0:CoLoadMasterBillNumber/text()"/>
          </xsl:when>
          <xsl:otherwise>
            <xsl:value-of select="s0:WayBillNumber/text()"/>
          </xsl:otherwise>
        </xsl:choose>
      </xsl:variable>
      <xsl:call-template name="RFF">
        <xsl:with-param name="code" select="'BM'"/>
        <xsl:with-param name="ref" select="$billNumber"/>
      </xsl:call-template>
      <xsl:variable name="cqnNumber" select="s0:AdditionalReferenceCollection/s0:AdditionalReference[s0:Type/s0:Code/text()='CQN']/s0:ReferenceNumber/text()" />
      <xsl:choose>
        <xsl:when test="$cqnNumber!=''">
          <xsl:for-each select="s0:AdditionalReferenceCollection/s0:AdditionalReference[s0:Type/s0:Code/text()='CQN']">
            <xsl:variable name="cqn" select="StringHelper:TrimCRLF(StringHelper:ShrinkCRLF(s0:ReferenceNumber/text()))" />
            <xsl:variable name="shouldAddCQN" select="ListHelper:ShouldCreateItem('CQN+', $cqn)"/>
            <xsl:if test="$shouldAddCQN">
              <xsl:call-template name="RFF">
                <xsl:with-param name="code" select="'CT'"/>
                <xsl:with-param name="ref" select="$cqn"/>
              </xsl:call-template>
            </xsl:if>
          </xsl:for-each>
        </xsl:when>
        <xsl:otherwise>
          <xsl:for-each select="s0:AdditionalReferenceCollection/s0:AdditionalReference[s0:Type/s0:Code/text()='CON']">
            <xsl:variable name="con" select="StringHelper:TrimCRLF(StringHelper:ShrinkCRLF(s0:ReferenceNumber/text()))" />
            <xsl:variable name="shouldAddCON" select="ListHelper:ShouldCreateItem('CON+', $con)"/>
            <xsl:if test="$shouldAddCON">
              <xsl:call-template name="RFF">
                <xsl:with-param name="code" select="'CT'"/>
                <xsl:with-param name="ref" select="$con"/>
              </xsl:call-template>
            </xsl:if>
          </xsl:for-each>
        </xsl:otherwise>
      </xsl:choose>
      <xsl:call-template name="RFF">
        <xsl:with-param name="code" select="'FF'"/>
        <xsl:with-param name="ref" select="$consolID"/>
      </xsl:call-template>
      <xsl:call-template name="RFF">
        <xsl:with-param name="code" select="'LC'"/>
        <xsl:with-param name="ref" select="s0:AdditionalReferenceCollection/s0:AdditionalReference[s0:Type/s0:Code/text()='LCR']/s0:ReferenceNumber/text()"/>
      </xsl:call-template>
      <xsl:variable name="useMessageReference" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'SID (RFF+SI) Value', 'Use Message Reference', $RecipientID)" />
      <xsl:variable name="refNumberOAG" select="s0:AdditionalReferenceCollection/s0:AdditionalReference[s0:Type/s0:Code/text()='OAG']/s0:ReferenceNumber/text()" />
      <xsl:variable name="RFF_SI">
        <xsl:choose>
          <xsl:when test="StringHelper:ToUpper($useMessageReference)='TRUE'">
            <xsl:value-of select="$SubscriberConsolReference" />
          </xsl:when>
          <xsl:otherwise>
            <xsl:value-of select="$refNumberOAG" />
          </xsl:otherwise>
        </xsl:choose>
      </xsl:variable>
      <xsl:call-template name="RFF">
        <xsl:with-param name="code" select="'SI'"/>
        <xsl:with-param name="ref" select="$RFF_SI"/>
      </xsl:call-template>
      <xsl:if test="StringHelper:ToUpper($useMessageReference)='TRUE'">
        <xsl:call-template name="RFF">
          <xsl:with-param name="code" select="'ERN'"/>
          <xsl:with-param name="ref" select="$refNumberOAG"/>
        </xsl:call-template>
      </xsl:if>
      <xsl:for-each select="(s0:AdditionalReferenceCollection/s0:AdditionalReference[s0:Type/s0:Code/text()='RUC']/s0:ReferenceNumber)/text()[not(text()=preceding::*/text())]">
        <xsl:call-template name="RFF">
          <xsl:with-param name="code" select="'ERN'"/>
          <xsl:with-param name="ref" select="."/>
        </xsl:call-template>
      </xsl:for-each>
      <xsl:for-each select="(s0:AdditionalReferenceCollection/s0:AdditionalReference[s0:Type/s0:Code/text()='ACI']/s0:ReferenceNumber)/text()[not(text()=preceding::*/text())]">
        <xsl:call-template name="RFF">
          <xsl:with-param name="code" select="'MCD'"/>
          <xsl:with-param name="ref" select="."/>
        </xsl:call-template>
      </xsl:for-each>

      <xsl:if test="$ICS2FillingType_CarrierWithOneSubShipment='TRUE' and $shipmentType != 'DRT'">
        <xsl:call-template name="RFF">
          <xsl:with-param name="code" select="'BH'"/>
          <xsl:with-param name="ref" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:WayBillNumber/text()"/>
        </xsl:call-template>
      </xsl:if>
      <xsl:if test="$ICS2FilingType = 'Declarant'">
        <xsl:call-template name="RFF">
          <xsl:with-param name="code" select="'AHP'"/>
          <xsl:with-param name="ref" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='ICS2DeclarantEORI']/s0:Value/text()"/>
        </xsl:call-template>
      </xsl:if>

      <xsl:for-each select="s0:PaymentHandlingInstructionCollection/s0:PaymentHandlingInstruction">
        <xsl:call-template name="CPI">
          <xsl:with-param name="paymentCategory" select="s0:Category/s0:Code/text()"/>
          <xsl:with-param name="paymentMethod" select="s0:PaymentMethod/s0:Code/text()"/>
        </xsl:call-template>
      </xsl:for-each>

      <ns0:TDTLoop1>
        <ns0:TDT>
          <TDT01>20</TDT01>
          <TDT02>
            <xsl:value-of select="$mainLeg/s0:VoyageFlightNo/text()"/>
          </TDT02>
          <ns0:C220>
            <C22001>1</C22001>
          </ns0:C220>
          <xsl:if test="$scac!=''">
            <ns0:C040>
              <C04001>
                <xsl:value-of select="$scac"/>
              </C04001>
              <C04002>172</C04002>
            </ns0:C040>
          </xsl:if>
          <ns0:C222>
            <xsl:variable name="lloyds" select="$mainLeg/s0:VesselLloydsIMO/text()"/>
            <xsl:if test="$lloyds!=''">
              <C22201>
                <xsl:value-of select="$lloyds"/>
              </C22201>
              <C22203>11</C22203>
            </xsl:if>
            <C22204>
              <xsl:value-of select="$mainLeg/s0:VesselName/text()"/>
            </C22204>
          </ns0:C222>
        </ns0:TDT>

        <xsl:for-each select="s0:TransportLegCollection/s0:TransportLeg">
          <xsl:variable name="findFirstAndLastLegOrder" select="userCSharp:FindFirstAndLastLegOrder(s0:LegOrder/text())"/>
        </xsl:for-each>
        <xsl:variable name="lastLegTransportMode" select="s0:TransportLegCollection/s0:TransportLeg[s0:LegOrder/text() = userCSharp:GetLastLegOrder()]/s0:TransportMode/text()"/>
        <xsl:variable name="firstLegTransportMode" select="s0:TransportLegCollection/s0:TransportLeg[s0:LegOrder/text() = userCSharp:GetFirstLegOrder()]/s0:TransportMode/text()"/>
        <xsl:variable name="isMSC" select="contains($RecipientID, 'MSC')"/>

        <xsl:if test="not($isMSC) or ($isMSC and (($lastLegTransportMode!='Sea' and ($TSRValue='28' or $TSRValue='30')) or $TSRValue='27' or $TSRValue='29'))">
          <xsl:call-template name="LOC2">
            <xsl:with-param name="code" select="'7'"/>
            <xsl:with-param name="node" select="s0:PlaceOfDelivery"/>
          </xsl:call-template>
        </xsl:if>
        <xsl:call-template name="LOC2">
          <xsl:with-param name="code" select="'9'"/>
          <xsl:with-param name="node" select="s0:PortOfLoading"/>
        </xsl:call-template>
        <xsl:call-template name="LOC2">
          <xsl:with-param name="code" select="'11'"/>
          <xsl:with-param name="node" select="s0:PortOfDischarge"/>
        </xsl:call-template>
        <xsl:if test="not($isMSC) or ($isMSC and (($firstLegTransportMode!='Sea' and ($TSRValue='29' or $TSRValue='30')) or $TSRValue='27' or $TSRValue='28'))">
          <xsl:call-template name="LOC2">
            <xsl:with-param name="code" select="'88'"/>
            <xsl:with-param name="node" select="s0:PlaceOfReceipt"/>
          </xsl:call-template>
        </xsl:if>
        <xsl:if test="not($shipmentType != 'DRT' and $ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE')">
          <xsl:call-template name="LOC2">
            <xsl:with-param name="code" select="'198'"/>
            <xsl:with-param name="node" select="s0:PortOfOrigin"/>
          </xsl:call-template>
        </xsl:if>
        <xsl:if test="StringHelper:ToUpper($ServiceProvider) != 'YANGMING'">
          <xsl:if test="$shipmentType != 'DRT' and $ICS2FillingType_CarrierWithOneSubShipment='TRUE'">
            <xsl:call-template name="LOC2">
              <xsl:with-param name="code" select="'83'"/>
              <xsl:with-param name="node" select="s0:PortOfDestination"/>
            </xsl:call-template>
          </xsl:if>
        </xsl:if>
      </ns0:TDTLoop1>

      <xsl:variable name="routingPartyRef" select="s0:AdditionalReferenceCollection/s0:AdditionalReference[s0:Type/s0:Code/text()='NAC']/s0:ReferenceNumber/text()"/>
      <xsl:if test="$routingPartyRef!=''">
        <ns0:NADLoop1>
          <ns0:NAD>
            <NAD01>FC</NAD01>
            <ns0:C082>
              <C08201>
                <xsl:value-of select="$routingPartyRef"/>
              </C08201>
              <C08202>160</C08202>
            </ns0:C082>
          </ns0:NAD>
        </ns0:NADLoop1>
      </xsl:if>

      <xsl:call-template name="NAD-Org">
        <xsl:with-param name="code" select="'CA'"/>
        <xsl:with-param name="org" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ShippingLineAddress']"/>
      </xsl:call-template>
      <xsl:call-template name="NAD-Org">
        <xsl:with-param name="code" select="'CN'"/>
        <xsl:with-param name="org" select="$consignee"/>
        <xsl:with-param name="countryOfIssue" select="$destinationPortPrefix"/>
      </xsl:call-template>
      <xsl:call-template name="NAD-Org">
        <xsl:with-param name="code" select="'FP'"/>
        <xsl:with-param name="org" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='FreightPayer']"/>
      </xsl:call-template>
      <xsl:call-template name="NAD-Org">
        <xsl:with-param name="code" select="'CZ'"/>
        <xsl:with-param name="inttraId" select="$nadCZClientID" />
        <xsl:with-param name="org" select="$consignor"/>
        <xsl:with-param name="countryOfIssue" select="$originPortPrefix"/>
      </xsl:call-template>
      <xsl:call-template name="NAD-Org">
        <xsl:with-param name="code" select="'FW'"/>
        <xsl:with-param name="inttraId" select="$nadFWClientID" />
        <xsl:with-param name="org" select="$bookingParty"/>
      </xsl:call-template>

      <xsl:choose>
        <xsl:when test="$ICS2FillingType_CarrierWithOneSubShipment='TRUE' and $shipmentType != 'DRT'">
          <xsl:call-template name="NAD-Org">
            <xsl:with-param name="code" select="'GO'"/>
            <xsl:with-param name="org" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsigneeDocumentaryAddress']"/>
            <xsl:with-param name="portOfDestination" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:PortOfDestination/s0:Code/text()"/>
          </xsl:call-template>
        </xsl:when>
        <xsl:when test="$ICS2FilingType = ''">
          <xsl:call-template name="NAD-Org">
            <xsl:with-param name="code" select="'GO'"/>
            <xsl:with-param name="org" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='BuyerDocumentaryAddress']"/>
            <xsl:with-param name="portOfDestination" select="s0:PortOfDestination/s0:Code/text()"/>
          </xsl:call-template>
        </xsl:when>
      </xsl:choose>

      <xsl:choose>
        <xsl:when test="concat($currentUser/s0:CompanyName/text(), $currentUser/s0:Address1/text())!=''">
          <xsl:call-template name="NAD-Org">
            <xsl:with-param name="code" select="'HI'"/>
            <xsl:with-param name="inttraId" select="$ClientID"/>
            <xsl:with-param name="org" select="$currentUser"/>
          </xsl:call-template>
        </xsl:when>
        <xsl:otherwise>
          <xsl:call-template name="NAD-Org">
            <xsl:with-param name="code" select="'HI'"/>
            <xsl:with-param name="inttraId" select="$ClientID"/>
            <xsl:with-param name="org" select="$bookingParty"/>
          </xsl:call-template>
        </xsl:otherwise>
      </xsl:choose>

      <xsl:call-template name="NAD-Org">
        <xsl:with-param name="code" select="'NI'"/>
        <xsl:with-param name="org" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='NotifyParty']"/>
        <xsl:with-param name="countryOfIssue" select="$destinationPortPrefix"/>
      </xsl:call-template>
      <xsl:call-template name="NAD-Org">
        <xsl:with-param name="code" select="'N1'"/>
        <xsl:with-param name="org" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='NotifyParty2']"/>
      </xsl:call-template>
      <xsl:call-template name="NAD-Org">
        <xsl:with-param name="code" select="'N2'"/>
        <xsl:with-param name="org" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='NotifyParty3']"/>
      </xsl:call-template>
      <xsl:call-template name="NAD-Org">
        <xsl:with-param name="code" select="'ST'"/>
        <xsl:with-param name="org" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsigneePickupDeliveryAddress']"/>
      </xsl:call-template>

      <xsl:if test="$ICS2FilingType = 'Carrier'">
        <xsl:choose>
          <xsl:when test="$shipmentType = 'DRT'">
            <xsl:variable name="supplierOrg">
              <xsl:call-template name="NAD-Org-Select">
                <xsl:with-param name="org1" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='SupplierDocumentaryAddress']"/>
                <xsl:with-param name="org2" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsignorDocumentaryAddress']"/>
              </xsl:call-template>
            </xsl:variable>
            <xsl:call-template name="NAD-Org">
              <xsl:with-param name="code" select="'SE'"/>
              <xsl:with-param name="org" select="msxsl:node-set($supplierOrg)/s0:OrganizationAddress"/>
            </xsl:call-template>
            <xsl:variable name="buyerOrg">
              <xsl:call-template name="NAD-Org-Select">
                <xsl:with-param name="org1" select="s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='BuyerDocumentaryAddress']"/>
                <xsl:with-param name="org2" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsigneeDocumentaryAddress']"/>
              </xsl:call-template>
            </xsl:variable>
            <xsl:variable name="buyerAddressCorrespondingPortOfDestination">
              <xsl:choose>
                <xsl:when test="concat(msxsl:node-set($buyerOrg)/s0:CompanyName/text(), msxsl:node-set($buyerOrg)/s0:Address1/text()) = concat(s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='BuyerDocumentaryAddress']/s0:CompanyName/text(), s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='BuyerDocumentaryAddress']/s0:Address1/text())">
                  <xsl:value-of select="s0:PortOfDestination/s0:Code/text()"/>
                </xsl:when>
                <xsl:otherwise>
                  <xsl:value-of select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:PortOfDestination/s0:Code/text()"/>
                </xsl:otherwise>
              </xsl:choose>
            </xsl:variable>
            <xsl:call-template name="NAD-Org">
              <xsl:with-param name="code" select="'BY'"/>
              <xsl:with-param name="org" select="msxsl:node-set($buyerOrg)/s0:OrganizationAddress"/>
              <xsl:with-param name="portOfDestination" select="$buyerAddressCorrespondingPortOfDestination"/>
            </xsl:call-template>
          </xsl:when>
          <xsl:when test="$subShipmentCount = 1">
            <xsl:variable name="supplierOrg">
              <xsl:call-template name="NAD-Org-Select">
                <xsl:with-param name="org1" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='SupplierDocumentaryAddress']"/>
                <xsl:with-param name="org2" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsignorDocumentaryAddress']"/>
              </xsl:call-template>
            </xsl:variable>
            <xsl:call-template name="NAD-Org">
              <xsl:with-param name="code" select="'SE'"/>
              <xsl:with-param name="org" select="msxsl:node-set($supplierOrg)/s0:OrganizationAddress"/>
            </xsl:call-template>
            <xsl:variable name="buyerOrg">
              <xsl:call-template name="NAD-Org-Select">
                <xsl:with-param name="org1" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='BuyerDocumentaryAddress']"/>
                <xsl:with-param name="org2" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsigneeDocumentaryAddress']"/>
              </xsl:call-template>
            </xsl:variable>
            <xsl:call-template name="NAD-Org">
              <xsl:with-param name="code" select="'BY'"/>
              <xsl:with-param name="org" select="msxsl:node-set($buyerOrg)/s0:OrganizationAddress"/>
              <xsl:with-param name="portOfDestination" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:PortOfDestination/s0:Code/text()"/>
            </xsl:call-template>
            <xsl:call-template name="NAD-Org">
              <xsl:with-param name="code" select="'EX'"/>
              <xsl:with-param name="org" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsignorDocumentaryAddress']"/>
            </xsl:call-template>
            <xsl:call-template name="NAD-Org">
              <xsl:with-param name="code" select="'ZZZ'"/>
              <xsl:with-param name="org" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='NotifyParty']"/>
              <xsl:with-param name="portOfDestination" select="s0:SubShipmentCollection/s0:SubShipment[1]/s0:PortOfDestination/s0:Code/text()"/>
            </xsl:call-template>
          </xsl:when>
        </xsl:choose>
      </xsl:if>

      <xsl:call-template name="GIDLoop">
        <xsl:with-param name="groupingMethod" select="$groupingMethod"/>
        <xsl:with-param name="segmentPackingLineCollection" select="s0:PackingLineCollection"/>
        <xsl:with-param name="unitOfWeight" select="$unitOfWeight"/>
        <xsl:with-param name="unitOfVolume" select="$unitOfVolume"/>
        <xsl:with-param name="dischargePortPrefix" select="$dischargePortPrefix"/>
        <xsl:with-param name="loadPortPrefix" select="$loadPortPrefix"/>
        <xsl:with-param name="numberFormat" select="$numberFormat"/>
      </xsl:call-template>

      <xsl:for-each select="s0:ContainerCollection/s0:Container">
        <xsl:variable name="containerTypeMappingV1" select="CodeMapper:GetRecipientCode($CarrierName , $CarrierName , $combineRecipientMappingNamev1, 'ContainerTypeToISOCode' , $combineRecipientCodeField,  s0:ContainerType/s0:ISOCode/text())"/>
        <xsl:variable name="ContainerTypeMappingCode">
          <xsl:choose>
            <xsl:when test="$containerTypeMappingV1 != ''">
              <xsl:value-of select="$containerTypeMappingV1"/>
            </xsl:when>
            <xsl:otherwise>
              <xsl:value-of select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION' , 'SHIPPING_INSTRUCTION' , 'OCM System Configuration', 'ContainerTypeToISOCode' , 'Carrier Code', s0:ContainerType/s0:ISOCode/text())"/>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:variable>

        <xsl:variable name="containerISOCode">
          <xsl:choose>
            <xsl:when test="$ContainerTypeMappingCode!=''">
              <xsl:value-of select="$ContainerTypeMappingCode"/>
            </xsl:when>
            <xsl:otherwise>
              <xsl:value-of select="s0:ContainerType/s0:ISOCode/text()"/>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:variable>

        <xsl:if test="not($addInfo_IsCoLoad = 'TRUE') or normalize-space(s0:ContainerNumber/text())!=''">
          <ns0:EQDLoop1>
            <ns0:EQD>
              <EQD01>CN</EQD01>
              <ns0:C237_3>
                <C23701>
                  <xsl:value-of select="s0:ContainerNumber/text()"/>
                </C23701>
              </ns0:C237_3>
              <ns0:C224>
                <C22401>
                  <xsl:value-of select="$containerISOCode"/>
                </C22401>
                <C22404>
                  <xsl:value-of select="s0:ContainerType/s0:Description/text()"/>
                </C22404>
              </ns0:C224>
              <EQD04>
                <xsl:choose>
                  <xsl:when test="s0:IsShipperOwned/text()='true'">1</xsl:when>
                  <xsl:otherwise>2</xsl:otherwise>
                </xsl:choose>
              </EQD04>
            </ns0:EQD>

            <xsl:variable name="isControlledAtmosphere" select="s0:IsControlledAtmosphere/text()"/>
            <xsl:variable name="airVentFlow" select="s0:AirVentFlow/text()"/>
            <xsl:if test="$isControlledAtmosphere='true' and $airVentFlow!=''">
              <xsl:variable name="airVentFlowRateUnit" select="s0:AirVentFlowRateUnit/s0:Code/text()"/>
              <xsl:variable name="airVentFlowValue">
                <xsl:choose>
                  <xsl:when test="$airVentFlowRateUnit='MQH' or $airVentFlowRateUnit='CBM' or $airVentFlowRateUnit='P1'">
                    <xsl:value-of select="StringMapper:FormatDecimal($airVentFlow, '0.##', false())"/>
                  </xsl:when>
                  <xsl:when test="$airVentFlowRateUnit='2L'">
                    <xsl:value-of select="userCSharp:MultiplyDouble($airVentFlow, '1.6990108', '0.##')"/>
                  </xsl:when>
                </xsl:choose>
              </xsl:variable>

              <xsl:if test="$airVentFlowValue!=''">
                <xsl:call-template name="MEA">
                  <xsl:with-param name="code" select="'AAE'"/>
                  <xsl:with-param name="type" select="'AAS'"/>
                  <xsl:with-param name="unit" select="'CBM'" />
                  <xsl:with-param name="value" select="$airVentFlowValue"/>
                  <xsl:with-param name="suffix" select="'_6'"/>
                </xsl:call-template>
              </xsl:if>
            </xsl:if>

            <xsl:variable name="grossVolume" select="userCSharp:GetTotalPacklineVolumeByContainerNumber(s0:ContainerNumber/text())"/>
            <xsl:if test="$grossVolume!=''">
              <xsl:call-template name="MEA">
                <xsl:with-param name="code" select="'AAE'"/>
                <xsl:with-param name="type" select="'AAW'"/>
                <xsl:with-param name="unit" select="$unitOfVolume"/>
                <xsl:with-param name="value" select="StringMapper:FormatDecimal($grossVolume,'0.####',false())"/>
                <xsl:with-param name="suffix" select="'_6'"/>
              </xsl:call-template>
            </xsl:if>

            <xsl:if test="s0:TareWeight/text()!=''">
              <xsl:call-template name="MEA">
                <xsl:with-param name="code" select="'AAE'"/>
                <xsl:with-param name="type" select="'T'"/>
                <xsl:with-param name="unit" select="$unitOfWeight"/>
                <xsl:with-param name="value" select="StringMapper:FormatDecimal(s0:TareWeight/text(),'0.###',false())"/>
                <xsl:with-param name="suffix" select="'_6'"/>
              </xsl:call-template>
            </xsl:if>

            <xsl:variable name="goodsWeight" select="userCSharp:GetTotalPacklineWeightByContainerNumber(s0:ContainerNumber/text())"/>
            <xsl:if test="$goodsWeight!=''">
              <xsl:call-template name="MEA">
                <xsl:with-param name="code" select="'AAE'"/>
                <xsl:with-param name="type" select="'WT'"/>
                <xsl:with-param name="unit" select="$unitOfWeight"/>
                <xsl:with-param name="value" select="StringMapper:FormatDecimal($goodsWeight,'0.###',false())"/>
                <xsl:with-param name="suffix" select="'_6'"/>
              </xsl:call-template>
            </xsl:if>

            <xsl:call-template name="SEL">
              <xsl:with-param name="sealNumber" select="s0:Seal/text()"/>
              <xsl:with-param name="sealParty" select="s0:SealPartyType/s0:Code/text()"/>
            </xsl:call-template>
            <xsl:call-template name="SEL">
              <xsl:with-param name="sealNumber" select="s0:SecondSeal/text()"/>
              <xsl:with-param name="sealParty" select="s0:SecondSealPartyType/s0:Code/text()"/>
            </xsl:call-template>
            <xsl:call-template name="SEL">
              <xsl:with-param name="sealNumber" select="s0:ThirdSeal/text()"/>
              <xsl:with-param name="sealParty" select="s0:ThirdSealPartyType/s0:Code/text()"/>
            </xsl:call-template>

            <xsl:if test="$isControlledAtmosphere='true' or contains($containerISOCode,'R')">
              <xsl:choose>
                <xsl:when test="$isControlledAtmosphere='true' and s0:ContainerType/s0:Category/s0:Code/text()='RFG'">
                  <xsl:variable name="setPointTemp">
                    <xsl:choose>
                      <xsl:when test="contains($numberFormat, 'TMP=')">
                        <xsl:variable name="numberFormatTMP" select="userCSharp:GetNumberFormat($numberFormat,'TMP=',1)" />
                        <xsl:value-of select="userCSharp:FormatTemp(StringMapper:FormatDecimal(s0:SetPointTemp/text(),$numberFormatTMP,false()))"/>
                      </xsl:when>
                      <xsl:otherwise>
                        <xsl:value-of select="userCSharp:FormatTemp(s0:SetPointTemp/text())"/>
                      </xsl:otherwise>
                    </xsl:choose>
                  </xsl:variable>
                  <ns0:TMP_2>
                    <TMP01>2</TMP01>
                    <ns0:C239_2>
                      <C23901>
                        <xsl:value-of select="$setPointTemp"/>
                      </C23901>
                      <C23902>
                        <xsl:choose>
                          <xsl:when test="s0:SetPointTempUnit/text()='C'">CEL</xsl:when>
                          <xsl:otherwise>FAH</xsl:otherwise>
                        </xsl:choose>
                      </C23902>
                    </ns0:C239_2>
                  </ns0:TMP_2>
                </xsl:when>
                <xsl:otherwise>
                  <ns0:TMP_2>
                    <TMP01>2</TMP01>
                    <ns0:C239_2>
                      <C23901>999</C23901>
                      <C23902>CEL</C23902>
                    </ns0:C239_2>
                  </ns0:TMP_2>
                </xsl:otherwise>
              </xsl:choose>
            </xsl:if>
          </ns0:EQDLoop1>
        </xsl:if>
      </xsl:for-each>

    </ns0:EFACT_D99B_IFTMIN>
  </xsl:template>

  <xsl:template name="CNT">
    <xsl:param name="code"/>
    <xsl:param name="value"/>
    <xsl:param name="measurementUnit"/>

    <xsl:if test="$value!='' and $value!=0">
      <ns0:CNT>
        <ns0:C270>
          <C27001>
            <xsl:value-of select="$code"/>
          </C27001>
          <C27002>
            <xsl:value-of select="$value"/>
          </C27002>
          <xsl:if test="$measurementUnit!=''">
            <C27003>
              <xsl:value-of select="$measurementUnit"/>
            </C27003>
          </xsl:if>
        </ns0:C270>
      </ns0:CNT>
    </xsl:if>
  </xsl:template>

  <xsl:template name="CPI">
    <xsl:param name="paymentCategory"/>
    <xsl:param name="paymentMethod"/>

    <xsl:variable name="cpiCode">
      <xsl:choose>
        <xsl:when test="$paymentCategory='FRT'">4</xsl:when>
        <xsl:when test="$paymentCategory='DHC'">5</xsl:when>
        <xsl:when test="$paymentCategory='DPC'">7</xsl:when>
        <xsl:when test="$paymentCategory='OPC'">10</xsl:when>
        <xsl:when test="$paymentCategory='OHC'">11</xsl:when>
        <xsl:otherwise></xsl:otherwise>
      </xsl:choose>
    </xsl:variable>

    <xsl:if test="$cpiCode!='' and $paymentCategory!='' and $paymentMethod!=''">
      <ns0:CPILoop1>
        <ns0:CPI>
          <ns0:C229>
            <C22901>
              <xsl:value-of select="$cpiCode"/>
            </C22901>
          </ns0:C229>
          <CPI03>
            <xsl:choose>
              <xsl:when test="$paymentMethod='PPD'">P</xsl:when>
              <xsl:otherwise>
                <xsl:variable name="payableElseWhereCode">
                  <xsl:choose>
                    <xsl:when test="$paymentCategory='FRT' and $paymentMethod='ELS'">
                      <xsl:value-of select="OCMHelper:GetPayableElseWhereOutputCode($ServiceProvider)" />
                    </xsl:when>
                    <xsl:otherwise></xsl:otherwise>
                  </xsl:choose>
                </xsl:variable>

                <xsl:choose>
                  <xsl:when test="$payableElseWhereCode!=''">
                    <xsl:value-of select="$payableElseWhereCode"/>
                  </xsl:when>
                  <xsl:otherwise>C</xsl:otherwise>
                </xsl:choose>
              </xsl:otherwise>
            </xsl:choose>
          </CPI03>
        </ns0:CPI>
      </ns0:CPILoop1>
    </xsl:if>
  </xsl:template>

  <xsl:template name="RFF">
    <xsl:param name="suffix" select="'1'"/>
    <xsl:param name="code"/>
    <xsl:param name="ref"/>
    <xsl:param name="regulatingCountry"/>

    <xsl:variable name="normSuffix">
      <xsl:choose>
        <xsl:when test="$suffix='3'">
          <xsl:value-of select="'_4'"/>
        </xsl:when>
        <xsl:when test="$suffix='4'">
          <xsl:value-of select="'_7'"/>
        </xsl:when>
        <xsl:otherwise>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>

    <xsl:if test="$ref!=''">
      <xsl:element name="ns0:RFFLoop{$suffix}">
        <xsl:call-template name="RFF-Core">
          <xsl:with-param name="suffix" select="$normSuffix"/>
          <xsl:with-param name="code" select="$code"/>
          <xsl:with-param name="value" select="$ref"/>
          <xsl:with-param name="regulatingCountry" select="$regulatingCountry"/>
        </xsl:call-template>
      </xsl:element>
    </xsl:if>
  </xsl:template>

  <xsl:template name="RFF-Core">
    <xsl:param name="suffix"/>
    <xsl:param name="code"/>
    <xsl:param name="value"/>
    <xsl:param name="regulatingCountry"/>

    <xsl:if test="$value!=''">
      <xsl:element name="ns0:RFF{$suffix}">
        <xsl:element name="ns0:C506{$suffix}">
          <C50601>
            <xsl:value-of select="$code"/>
          </C50601>
          <C50602>
            <xsl:value-of select="$value"/>
          </C50602>
          <xsl:if test="$regulatingCountry != ''">
            <C50603>
              <xsl:value-of select="$regulatingCountry"/>
            </C50603>
          </xsl:if>
        </xsl:element>
      </xsl:element>
    </xsl:if>
  </xsl:template>

  <xsl:template name="RFF-TN">
    <xsl:param name="ITNNumber" />
    <xsl:param name="DUENumber" />
    <xsl:param name="exportReferenceNumber" />
    <xsl:param name="CTNNumber" />
    <xsl:param name="MRNNumber" />

    <xsl:variable name="number">
      <xsl:call-template name="GetValueOrDefault">
        <xsl:with-param name="value1" select="$ITNNumber"/>
        <xsl:with-param name="value2" select="$DUENumber"/>
        <xsl:with-param name="value3" select="$exportReferenceNumber"/>
        <xsl:with-param name="value4" select="$CTNNumber"/>
        <xsl:with-param name="value5" select="$MRNNumber"/>
      </xsl:call-template>
    </xsl:variable>

    <xsl:if test="$number != ''">
      <xsl:variable name="delimiter">
        <xsl:choose>
          <xsl:when test="contains($number, ';')">;</xsl:when>
          <xsl:when test="contains($number, ',')">,</xsl:when>
          <xsl:when test="contains($number, '/')">/</xsl:when>
          <xsl:when test="contains($number, '|')">|</xsl:when>
          <xsl:otherwise></xsl:otherwise>
        </xsl:choose>
      </xsl:variable>

      <xsl:for-each select="XMLHelper:ConvertToXMLNodes($number, 'Number', $delimiter)">
        <xsl:variable name="refNumber" select="normalize-space(text())"/>
        <xsl:if test="$refNumber!='' and ListHelperClear:ShouldCreateItem('SG22RFF+', concat('TN+', $refNumber), 9)">
          <xsl:call-template name="RFF">
            <xsl:with-param name="code" select="'TN'" />
            <xsl:with-param name="ref" select="$refNumber"/>
            <xsl:with-param name="suffix" select="'4'"/>
          </xsl:call-template>
        </xsl:if>
      </xsl:for-each>
    </xsl:if>
  </xsl:template>

  <xsl:template name="FTX-CCI-FixedTextValues">
    <xsl:param name="type"/>
    <xsl:param name="code"/>
    <xsl:param name="suffix"/>
    <xsl:param name="text1"/>
    <xsl:param name="text2"/>
    <xsl:param name="text3"/>
    <xsl:param name="text4"/>
    <xsl:param name="text5"/>

    <xsl:if test="userCSharp:ShouldCreateFTX() and not($code = 'AAA' and $text1 = '')">
      <xsl:element name="ns0:FTX{$suffix}">
        <FTX01>
          <xsl:value-of select="$type"/>
        </FTX01>
        <xsl:if test="$code != ''">
          <xsl:element name="ns0:C107{$suffix}">
            <C10701>
              <xsl:value-of select="$code"/>
            </C10701>
          </xsl:element>
        </xsl:if>
        <xsl:if test="$text1 != ''">
          <xsl:element name="ns0:C108{$suffix}">
            <C10801>
              <xsl:value-of select="$text1"/>
            </C10801>
            <xsl:if test="$text2 != ''">
              <C10802>
                <xsl:value-of select="$text2"/>
              </C10802>
              <xsl:if test="$text3 != ''">
                <C10803>
                  <xsl:value-of select="$text3"/>
                </C10803>
                <xsl:if test="$text4 != ''">
                  <C10804>
                    <xsl:value-of select="$text4"/>
                  </C10804>
                  <xsl:if test="$text5 != ''">
                    <C10805>
                      <xsl:value-of select="$text5"/>
                    </C10805>
                  </xsl:if>
                </xsl:if>
              </xsl:if>
            </xsl:if>
          </xsl:element>
        </xsl:if>
      </xsl:element>
      <xsl:variable name="setFTXCounter" select="userCSharp:SetFTXCounter()" />
    </xsl:if>
  </xsl:template>

  <xsl:template name="FTX-Core">
    <xsl:param name="type"/>
    <xsl:param name="code"/>
    <xsl:param name="text"/>
    <xsl:param name="noOfSegment"/>
    <xsl:param name="linelen"/>
    <xsl:param name="suffix"/>
    <xsl:param name="allowEmptySegment" select="'FALSE'"/>

    <xsl:variable name="normaltext" select="normalize-space($text)"/>
    <xsl:if test="$normaltext != '' or StringHelper:ToUpper($allowEmptySegment) = 'TRUE'">
      <xsl:element name="ns0:FTX{$suffix}">
        <FTX01>
          <xsl:value-of select="$type"/>
        </FTX01>
        <xsl:if test="$code!=''">
          <xsl:element name="ns0:C107{$suffix}">
            <C10701>
              <xsl:value-of select="$code"/>
            </C10701>
          </xsl:element>
        </xsl:if>
        <xsl:element name="ns0:C108{$suffix}">
          <xsl:variable name="resetFTXSegmentCounter" select="userCSharp:ResetFTXSegmentCounter()" />
          <xsl:if test="userCSharp:ShouldCreateFTXSegment($noOfSegment)">
            <xsl:call-template name="FTX-Segment">
              <xsl:with-param name="text" select="$normaltext"/>
              <xsl:with-param name="noOfSegment" select="$noOfSegment"/>
              <xsl:with-param name="linelen" select="$linelen"/>
            </xsl:call-template>
          </xsl:if>
        </xsl:element>
      </xsl:element>
      <xsl:variable name="setFTXCounter" select="userCSharp:SetFTXCounter()" />
    </xsl:if>
  </xsl:template>

  <xsl:template name="FTX-Segment">
    <xsl:param name="text"/>
    <xsl:param name="noOfSegment"/>
    <xsl:param name="linelen"/>

    <xsl:variable name="segmentCounter" select="userCSharp:GetFTXSegmentCounter()" />
    <xsl:element name="C1080{$segmentCounter}">
      <xsl:value-of select="normalize-space(userCSharp:GetFTXFormat($segmentCounter,$linelen,$text))"/>
    </xsl:element>
    <xsl:if test="userCSharp:ShouldCreateFTXSegment($noOfSegment)">
      <xsl:call-template name="FTX-Segment">
        <xsl:with-param name="text" select="$text"/>
        <xsl:with-param name="noOfSegment" select="$noOfSegment"/>
        <xsl:with-param name="linelen" select="$linelen"/>
      </xsl:call-template>
    </xsl:if>
  </xsl:template>

  <xsl:template name="FTX">
    <xsl:param name="type"/>
    <xsl:param name="text"/>
    <xsl:param name="code"/>
    <xsl:param name="suffix"/>
    <xsl:param name="allowEmptySegment" select="'FALSE'"/>

    <xsl:variable name="maxLength" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'FTX Format', 'MaxLength', $RecipientID, $OrgSCAC, $type)"/>

    <xsl:variable name="lineLength">
      <xsl:choose>
        <xsl:when test="$maxLength!=''">
          <xsl:value-of select="$maxLength"/>
        </xsl:when>
        <xsl:otherwise>512</xsl:otherwise>
      </xsl:choose>
    </xsl:variable>

    <xsl:variable name="noOfSegment" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'FTX Format', 'NoOfSegment', $RecipientID, $OrgSCAC, $type)"/>
    <xsl:variable name="segment">
      <xsl:choose>
        <xsl:when test="$noOfSegment!=''">
          <xsl:value-of select="$noOfSegment"/>
        </xsl:when>
        <xsl:otherwise>1</xsl:otherwise>
      </xsl:choose>
    </xsl:variable>

    <xsl:if test="userCSharp:ShouldCreateFTX()">
      <xsl:variable name="normText" select="normalize-space($text)" />

      <xsl:if test="$normText != '' or StringHelper:ToUpper($allowEmptySegment) = 'TRUE'">
        <xsl:variable name="ftxText" select="StringHelper:SubstringSafe($normText, 0, $segment * $lineLength)" />
        <xsl:call-template name="FTX-Core">
          <xsl:with-param name="type" select="$type" />
          <xsl:with-param name="code" select="$code" />
          <xsl:with-param name="text" select="$ftxText"/>
          <xsl:with-param name="noOfSegment" select="$segment"/>
          <xsl:with-param name="linelen" select="$lineLength"/>
          <xsl:with-param name="suffix" select="$suffix"/>
          <xsl:with-param name="allowEmptySegment" select="$allowEmptySegment"/>
        </xsl:call-template>

        <xsl:variable name="remainingFtxText" select="StringHelper:SubstringSafe($normText, $segment * $lineLength)" />
        <xsl:if test="$remainingFtxText!=''">
          <xsl:call-template name="FTX">
            <xsl:with-param name="type" select="$type" />
            <xsl:with-param name="code" select="$code" />
            <xsl:with-param name="text" select="$remainingFtxText" />
            <xsl:with-param name="suffix" select="$suffix"/>
            <xsl:with-param name="allowEmptySegment" select="$allowEmptySegment"/>
          </xsl:call-template>
        </xsl:if>
      </xsl:if>
    </xsl:if>
  </xsl:template>

  <xsl:template name="MEA">
    <xsl:param name="code"/>
    <xsl:param name="type"/>
    <xsl:param name="unit"/>
    <xsl:param name="value"/>
    <xsl:param name="suffix"/>

    <xsl:element name="ns0:MEA{$suffix}">
      <MEA01>
        <xsl:value-of select="$code"/>
      </MEA01>
      <xsl:element name="ns0:C502{$suffix}">
        <C50201>
          <xsl:value-of select="$type"/>
        </C50201>
      </xsl:element>
      <xsl:element name="ns0:C174{$suffix}">
        <C17401>
          <xsl:value-of select="$unit"/>
        </C17401>
        <C17402>
          <xsl:value-of select="$value" />
        </C17402>
      </xsl:element>
    </xsl:element>
  </xsl:template>

  <xsl:template name="FTX-BLC-Code">
    <xsl:param name="code"/>
    <xsl:param name="description"/>
    <xsl:call-template name="FTX">
      <xsl:with-param name="type" select="'BLC'"/>
      <xsl:with-param name="text" select="$description"/>
      <xsl:with-param name="code">
        <xsl:choose>
          <xsl:when test="$code='SLC'">01</xsl:when>
          <xsl:when test="$code='LSC'">02</xsl:when>
          <xsl:when test="$code='LOB'">03</xsl:when>
          <xsl:when test="$code='LBV'">04</xsl:when>
          <xsl:when test="$code='FPP'">17</xsl:when>
          <xsl:when test="$code='FCL'">18</xsl:when>
          <xsl:when test="$code='FAA'">19</xsl:when>
          <xsl:when test="$code='NSD'">20</xsl:when>
          <xsl:when test="$code='OBR'">22</xsl:when>
          <xsl:when test="$code='OBV'">24</xsl:when>
          <xsl:when test="$code='RFS'">25</xsl:when>
          <xsl:when test="$code='LNV'">34</xsl:when>
        </xsl:choose>
      </xsl:with-param>
    </xsl:call-template>
  </xsl:template>

  <xsl:template name="LOC1">
    <xsl:param name="code"/>
    <xsl:param name="unloco"/>
    <xsl:param name="name"/>
    <xsl:param name="date"/>
    <xsl:param name="dtmFormatCode" select="'95'"/>
    <xsl:if test="$unloco!='' or $code='57' ">
      <ns0:LOCLoop1>
        <xsl:call-template name="LOC">
          <xsl:with-param name="code" select="$code"/>
          <xsl:with-param name="unloco" select="$unloco"/>
          <xsl:with-param name="name" select="$name"/>
        </xsl:call-template>
        <xsl:if test="$date!=''">
          <ns0:DTM_2>
            <ns0:C507_2>
              <C50701>95</C50701>
              <C50702>
                <xsl:choose>
                  <xsl:when test="$dtmFormatCode='102'">
                    <xsl:value-of select="DateMapper:ConvertToDate($date,'yyyyMMdd')"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:value-of select="DateMapper:ConvertToDate($date,'yyyyMMddHHmm')"/>
                  </xsl:otherwise>
                </xsl:choose>
              </C50702>
              <C50703>
                <xsl:value-of select="$dtmFormatCode"/>
              </C50703>
            </ns0:C507_2>
          </ns0:DTM_2>
        </xsl:if>
      </ns0:LOCLoop1>
    </xsl:if>
  </xsl:template>

  <xsl:template name="LOC2">
    <xsl:param name="code"/>
    <xsl:param name="node"/>
    <xsl:variable name="unloco" select="$node/s0:Code/text()"/>
    <xsl:variable name="name" select="$node/s0:Name/text()"/>
    <xsl:if test="$unloco!=''">
      <ns0:LOCLoop2>
        <xsl:call-template name="LOC">
          <xsl:with-param name="code" select="$code"/>
          <xsl:with-param name="unloco" select="$unloco"/>
          <xsl:with-param name="name" select="$name"/>
          <xsl:with-param name="suffix" select="'_6'"/>
        </xsl:call-template>
      </ns0:LOCLoop2>
    </xsl:if>
  </xsl:template>

  <xsl:template name="LOC">
    <xsl:param name="code"/>
    <xsl:param name="unloco"/>
    <xsl:param name="name"/>
    <xsl:param name="suffix"/>
    <xsl:element name="ns0:LOC{$suffix}">
      <LOC01>
        <xsl:value-of select="$code"/>
      </LOC01>
      <xsl:element name="ns0:C517{$suffix}">
        <xsl:if test="$unloco!=''">
          <C51701>
            <xsl:value-of select="$unloco"/>
          </C51701>
          <C51703>6</C51703>
        </xsl:if>
        <C51704>
          <xsl:value-of select="$name"/>
        </C51704>
      </xsl:element>
      <xsl:if test="$unloco!=''">
        <xsl:element name="ns0:C519{$suffix}">
          <C51901>
            <xsl:value-of select="substring($unloco,1,2)"/>
          </C51901>
        </xsl:element>
      </xsl:if>
    </xsl:element>
  </xsl:template>

  <xsl:template name="NAD">
    <xsl:param name="code"/>
    <xsl:param name="id"/>
    <xsl:param name="name"/>
    <xsl:param name="street1"/>
    <xsl:param name="street2"/>
    <xsl:param name="city"/>
    <xsl:param name="state"/>
    <xsl:param name="postcode"/>
    <xsl:param name="country"/>
    <xsl:param name="countrycode"/>
    <xsl:param name="contact"/>
    <xsl:param name="phone"/>
    <xsl:param name="email"/>
    <xsl:param name="fax"/>
    <xsl:param name="suffix" select="''"/>
    <xsl:param name="registrationNumberCollection"/>
    <xsl:param name="portOfDestination" select ="''"/>

    <xsl:variable name="rootSuffix">
      <xsl:choose>
        <xsl:when test="$suffix = ''">1</xsl:when>
        <xsl:otherwise>
          <xsl:value-of select="translate($suffix, '_', '')"/>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>

    <xsl:variable name="idnorm" select="normalize-space($id)"/>
    <xsl:variable name="namenorm" select="normalize-space($name)"/>
    <xsl:variable name="street1norm" select="normalize-space($street1)"/>
    <xsl:variable name="street2norm" select="normalize-space($street2)"/>
    <xsl:variable name="citynorm" select="normalize-space($city)"/>
    <xsl:variable name="statenorm" select="normalize-space($state)"/>
    <xsl:variable name="postcodenorm" select="normalize-space($postcode)"/>
    <xsl:variable name="countrynorm" select="normalize-space($country)"/>
    <xsl:variable name="countrycodenorm" select="normalize-space($countrycode)"/>
    <xsl:variable name="contactnorm" select="normalize-space($contact)"/>
    <xsl:variable name="phonenorm" select="normalize-space($phone)"/>
    <xsl:variable name="emailnorm" select="normalize-space($email)"/>
    <xsl:variable name="faxnorm" select="normalize-space($fax)"/>

    <xsl:variable name="ics2Qualifiers">
      <xsl:choose>
        <xsl:when test="$suffix = '' and $ICS2FilingType = 'Carrier'">
          <xsl:variable name="defaultQualifiers" select="'GO;EX;SE;BY;ZZZ'"/>
          <xsl:choose>
            <xsl:when test="contains($RecipientID, 'HYUNDAI')">
              <xsl:variable name="consigneeCompanyName" select="$shipment/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text() = 'ConsigneeDocumentaryAddress']/s0:CompanyName/text()"/>
              <xsl:choose>
                <xsl:when test="not($consigneeCompanyName) or $consigneeCompanyName = '' or userCSharp:IsToOrder($consigneeCompanyName)">
                  <xsl:value-of select="concat('NI;', $defaultQualifiers)"/>
                </xsl:when>
                <xsl:otherwise>
                  <xsl:value-of select="concat('CN;', $defaultQualifiers)"/>
                </xsl:otherwise>
              </xsl:choose>
            </xsl:when>
            <xsl:otherwise>
              <xsl:value-of select="$defaultQualifiers"/>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:when>
        <xsl:when test="$suffix = '_2' and $ICS2FilingType != ''">
          <xsl:value-of select="'SE;BY;ZZZ;OS;UC'"/>
        </xsl:when>
        <xsl:otherwise></xsl:otherwise>
      </xsl:choose>
    </xsl:variable>

    <xsl:variable name="excludedSegment">
      <xsl:choose>
        <xsl:when test="($suffix = '' and $ICS2FilingType = 'Carrier' or $suffix = '_2' and $ICS2FilingType != '') and StringHelper:ContainsAny($ics2Qualifiers, $code)"></xsl:when>
        <xsl:otherwise>
          <xsl:value-of select="$AddresFormatExclusion"/>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>

    <xsl:if test="concat($idnorm,$namenorm,$street1norm,$street2norm)!=''">
      <xsl:element name="ns0:NADLoop{$rootSuffix}">
        <xsl:element name="ns0:NAD{$suffix}">
          <xsl:variable name="customaddr" select="userCSharp:GetCustomAddress($code, $namenorm, $street1norm, $street2norm)"/>
          <xsl:choose>
            <xsl:when test="$customaddr != '' and not(contains($excludedSegment, 'C080'))">
              <NAD01>
                <xsl:value-of select="$code"/>
              </NAD01>
              <xsl:element name="ns0:C080{$suffix}">
                <xsl:variable name="setupStringWrapper" select="StringWrapper:SetupStringWrapper(35, 5)" />
                <xsl:variable name="wrapStringIntoLines" select="StringWrapper:SplitStringInWrapper(StringHelper:ShrinkSpaces($customaddr))" />
                <xsl:variable name="c08001" select="normalize-space(StringWrapper:GetTextFromCurrentIndex())" />
                <xsl:variable name="c08002" select="normalize-space(StringWrapper:GetTextFromCurrentIndex())" />

                <C08001>
                  <xsl:value-of select="$c08001"/>
                </C08001>
                <xsl:if test="$c08002 != ''">
                  <C08002>
                    <xsl:value-of select="$c08002"/>
                  </C08002>
                </xsl:if>
              </xsl:element>
            </xsl:when>
            <xsl:otherwise>
              <NAD01>
                <xsl:value-of select="$code"/>
              </NAD01>
              <xsl:if test="$idnorm!='' and not(contains($excludedSegment, 'C082'))">
                <xsl:element name="ns0:C082{$suffix}">
                  <C08201>
                    <xsl:choose>
                      <xsl:when test="contains($idnorm, '+') and ($code = 'UC' or $code = 'BY' or $code = 'ZZZ')">
                        <xsl:value-of select="substring-after($idnorm, '+')"/>
                      </xsl:when>
                      <xsl:otherwise>
                        <xsl:value-of select="$idnorm"/>
                      </xsl:otherwise>
                    </xsl:choose>
                  </C08201>
                  <xsl:choose>
                    <xsl:when test="$code = 'UC' or $code = 'BY' or $code = 'ZZZ'">
                      <C08202>167</C08202>
                      <C08203>140</C08203>
                    </xsl:when>
                    <xsl:otherwise>
                      <C08202>160</C08202>
                      <xsl:choose>
                        <xsl:when test="contains('HI CA FW CZ', $code) and contains($RecipientID, 'MSC')">
                          <C08203>87</C08203>
                        </xsl:when>
                        <xsl:otherwise>
                          <C08203>86</C08203>
                        </xsl:otherwise>
                      </xsl:choose>
                    </xsl:otherwise>
                  </xsl:choose>
                </xsl:element>
              </xsl:if>
              <xsl:choose>
                <xsl:when test="($code = 'CA') and contains($RecipientID, 'MSC')"></xsl:when>
                <xsl:otherwise>
                  <xsl:if test="not(contains($excludedSegment, 'C058'))">
                    <xsl:variable name="address" select="userCSharp:ShrinkSpaces(concat($street1norm, ' ', $street2norm))"/>

                    <xsl:variable name="address1Index" select="userCSharp:GetEndIndex($address, 0) + 1"/>
                    <xsl:variable name="address2Index" select="userCSharp:GetEndIndex($address, $address1Index) + 1"/>

                    <xsl:element name="ns0:C058{$suffix}">
                      <C05801>
                        <xsl:value-of select="normalize-space(userCSharp:SubstringSafe($address, 0, $address1Index))"/>
                      </C05801>
                      <C05802>
                        <xsl:value-of select="normalize-space(userCSharp:SubstringSafe($address, $address1Index, $address2Index))"/>
                      </C05802>
                      <xsl:if test="$address!='' and StringHelper:ContainsAny($ics2Qualifiers, $code)">
                        <C05803>0</C05803>
                      </xsl:if>
                    </xsl:element>
                  </xsl:if>
                  <xsl:if test="not(contains($excludedSegment, 'C080'))">
                    <xsl:variable name="nameIndex1" select="userCSharp:GetEndIndex($namenorm, 0) + 1"/>
                    <xsl:variable name="nameIndex2" select="userCSharp:GetEndIndex($namenorm, $nameIndex1) + 1"/>
                    <xsl:variable name="address" select="userCSharp:ShrinkSpaces(concat($street1norm, $street2norm))"/>

                    <xsl:element name="ns0:C080{$suffix}">
                      <C08001>
                        <xsl:value-of select="normalize-space(userCSharp:SubstringSafe($namenorm, 0, $nameIndex1))" />
                      </C08001>
                      <C08002>
                        <xsl:value-of select="normalize-space(userCSharp:SubstringSafe($namenorm, $nameIndex1, $nameIndex2))" />
                      </C08002>
                      <!--WI00861480 :
                          VPL:  I had a discussion with Wayne.
                                Since we have already hardcoded the Street Number as "0" and the customer system does not support Street Number and P.O. BOX simultaneously,
                                the best approach will be to comment out the GetPOBox logic.
                      <xsl:if test="$ICS2FilingType != '' and StringHelper:ContainsAny('GO;EX;SE;BY;ZZZ;OS;UC', $code)">
                        <C08003>
                          <xsl:value-of select="ICS2Helper:GetPOBox($address)"/>
                        </C08003>
                      </xsl:if>-->
                      <xsl:if test="$suffix = '_2' and $code = 'ZZZ' and $phonenorm != ''">
                        <C08004>
                          <xsl:value-of select="$phonenorm"/>
                        </C08004>
                      </xsl:if>
                    </xsl:element>
                  </xsl:if>
                  <xsl:if test="not(contains($excludedSegment, 'C059'))">
                    <xsl:variable name="citystatepostcodenorm" select="normalize-space(concat($citynorm,' ',$statenorm, ' ', $postcodenorm))"/>
                    <xsl:variable name="street" select="userCSharp:ShrinkSpaces(concat($street1norm, ' ', $street2norm, ' ', $citystatepostcodenorm, ' ', $countrynorm))" />

                    <xsl:variable name="streetIndex1" select="userCSharp:GetEndIndex($street, 0) + 1"/>
                    <xsl:variable name="streetIndex2" select="userCSharp:GetEndIndex($street, $streetIndex1) + 1"/>
                    <xsl:variable name="streetIndex3" select="userCSharp:GetEndIndex($street, $streetIndex1 + $streetIndex2) + 1"/>
                    <xsl:variable name="streetIndex4" select="userCSharp:GetEndIndex($street, $streetIndex1 + $streetIndex2 + $streetIndex3) + 1"/>

                    <xsl:element name="ns0:C059{$suffix}">
                      <C05901>
                        <xsl:value-of select="normalize-space(userCSharp:SubstringSafe($street, 0, $streetIndex1))" />
                      </C05901>
                      <C05902>
                        <xsl:value-of select="normalize-space(userCSharp:SubstringSafe($street, $streetIndex1 , $streetIndex2))" />
                      </C05902>
                      <C05903>
                        <xsl:value-of select="normalize-space(userCSharp:SubstringSafe($street, $streetIndex1 + $streetIndex2, $streetIndex3))" />
                      </C05903>
                      <C05904>
                        <xsl:value-of select="normalize-space(userCSharp:SubstringSafe($street, $streetIndex1 + $streetIndex2 + $streetIndex3, $streetIndex4))" />
                      </C05904>
                    </xsl:element>
                  </xsl:if>

                  <xsl:if test="not(contains($excludedSegment, 'NAD06'))">
                    <NAD06>
                      <xsl:value-of select="normalize-space(substring($citynorm,1,35))"/>
                    </NAD06>
                  </xsl:if>
                  <xsl:if test="not(contains($excludedSegment, 'C819'))">
                    <xsl:element name="ns0:C819{$suffix}">
                      <C81901>
                        <xsl:value-of select="normalize-space(substring($statenorm,1,9))"/>
                      </C81901>
                    </xsl:element>
                  </xsl:if>
                  <xsl:if test="not(contains($excludedSegment, 'NAD08'))">
                    <NAD08>
                      <xsl:value-of select="$postcodenorm"/>
                    </NAD08>
                  </xsl:if>
                  <xsl:if test="not(contains($excludedSegment, 'NAD09'))">
                    <NAD09>
                      <xsl:value-of select="$countrycodenorm"/>
                    </NAD09>
                  </xsl:if>
                </xsl:otherwise>
              </xsl:choose>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:element>

        <xsl:if test="$suffix = '' and $contactnorm!='' and ($phonenorm!='' or $emailnorm!='' or $faxnorm!='')">
          <ns0:CTALoop1>
            <ns0:CTA_2>
              <CTA01>IC</CTA01>
              <ns0:C056_2>
                <C05602>
                  <xsl:value-of select="normalize-space(substring($contactnorm,1,35))"/>
                </C05602>
              </ns0:C056_2>
            </ns0:CTA_2>
            <xsl:if test="$phonenorm!=''">
              <ns0:COM_2>
                <ns0:C076_2>
                  <C07601>
                    <xsl:value-of select="$phonenorm"/>
                  </C07601>
                  <C07602>TE</C07602>
                </ns0:C076_2>
              </ns0:COM_2>
            </xsl:if>
            <xsl:if test="$emailnorm!=''">
              <ns0:COM_2>
                <ns0:C076_2>
                  <C07601>
                    <xsl:value-of select="$emailnorm"/>
                  </C07601>
                  <C07602>EM</C07602>
                </ns0:C076_2>
              </ns0:COM_2>
            </xsl:if>
            <xsl:if test="$faxnorm!=''">
              <ns0:COM_2>
                <ns0:C076_2>
                  <C07601>
                    <xsl:value-of select="$faxnorm"/>
                  </C07601>
                  <C07602>FX</C07602>
                </ns0:C076_2>
              </ns0:COM_2>
            </xsl:if>
          </ns0:CTALoop1>
        </xsl:if>

        <xsl:if test="$suffix = '' and $code='HI'">
          <xsl:variable name="releaseType" select="s0:ReleaseType/s0:Code/text()"/>
          <xsl:variable name="doccode">
            <xsl:choose>
              <xsl:when test="$releaseType='HBL'">714</xsl:when>
              <xsl:when test="$releaseType='SWB'">710</xsl:when>
              <xsl:when test="$releaseType='BOL' or $releaseType='EOB'">706</xsl:when>
            </xsl:choose>
          </xsl:variable>
          <xsl:if test="$doccode!=''">
            <ns0:DOCLoop2>
              <xsl:if test="$doccode!=''">
                <ns0:DOC_3>
                  <ns0:C002_4>
                    <C00201>
                      <xsl:value-of select="$doccode"/>
                    </C00201>
                    <xsl:if test="$doccode='714' and s0:ContainerMode/s0:Code/text()='LCL'">
                      <C00204>
                        <xsl:value-of select="s0:WayBillNumber/text()"/>
                      </C00204>
                    </xsl:if>
                  </ns0:C002_4>
                  <xsl:if test="$doccode!='714' and s0:ContainerMode/s0:Code/text()!='LCL'">
                    <ns0:C503_3>
                      <C50302>
                        <xsl:choose>
                          <xsl:when test="s0:NoteCollection/s0:Note[s0:Description/text()='ChargesFreighted']/s0:NoteText/text()='Y'">27</xsl:when>
                          <xsl:otherwise>26</xsl:otherwise>
                        </xsl:choose>
                      </C50302>
                    </ns0:C503_3>
                  </xsl:if>
                  <xsl:variable name="NoOfOriginalsOrCopies">
                    <xsl:choose>
                      <xsl:when test="$releaseType='SWB' or $releaseType='EBL' or $releaseType='NON'">
                        <xsl:value-of select="s0:NoCopyBills/text()"/>
                      </xsl:when>
                      <xsl:otherwise>
                        <xsl:value-of select="s0:NoOriginalBills/text()"/>
                      </xsl:otherwise>
                    </xsl:choose>
                  </xsl:variable>
                  <DOC04>
                    <xsl:value-of select="$NoOfOriginalsOrCopies"/>
                  </DOC04>
                </ns0:DOC_3>
              </xsl:if>
            </ns0:DOCLoop2>
          </xsl:if>

          <xsl:if test="($releaseType='BOL' or $releaseType='EOB') and s0:NoCopyBills/text() > 0">
            <ns0:DOCLoop2>
              <ns0:DOC_3>
                <ns0:C002_4>
                  <C00201>707</C00201>
                </ns0:C002_4>
                <ns0:C503_3>
                  <C50302>
                    <xsl:choose>
                      <xsl:when test="s0:NoteCollection/s0:Note[s0:Description/text()='ChargesFreighted']/s0:NoteText/text()='Y'">27</xsl:when>
                      <xsl:otherwise>26</xsl:otherwise>
                    </xsl:choose>
                  </C50302>
                </ns0:C503_3>
                <DOC04>
                  <xsl:value-of select="s0:NoCopyBills/text()"/>
                </DOC04>
              </ns0:DOC_3>
            </ns0:DOCLoop2>
          </xsl:if>
        </xsl:if>

        <xsl:if test="$suffix = '' and $registrationNumberCollection and ($code = 'GO' or $code = 'BY' or $code = 'ZZZ')">
          <xsl:variable name="eori" select="$registrationNumberCollection/s0:RegistrationNumber[s0:Type/s0:Code/text() = 'EOR']/s0:Value/text()"/>
          <xsl:variable name="eoriRegulatingCountry" select="$registrationNumberCollection/s0:RegistrationNumber[s0:Type/s0:Code/text() = 'EOR']/s0:RegulatingCountry/text()"/>
          <xsl:if test="$eori != '' and ICS2Helper:IsICS2Port($portOfDestination)">
            <xsl:call-template name="RFF">
              <xsl:with-param name="suffix" select="'3'"/>
              <xsl:with-param name="code" select="'GN'"/>
              <xsl:with-param name="ref">
                <xsl:choose>
                  <xsl:when test="contains($eori, '+')">
                    <xsl:value-of select="substring-after($eori, '+')"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:value-of select="$eori"/>
                  </xsl:otherwise>
                </xsl:choose>
              </xsl:with-param>
              <xsl:with-param name="regulatingCountry" select="$eoriRegulatingCountry"/>
            </xsl:call-template>
          </xsl:if>
        </xsl:if>

        <xsl:if test="$suffix = '' and MultipleRFFListHelper:RFFGNCount() > 0 ">
          <xsl:for-each select="MultipleRFFListHelper:RFFGNCollection()">
            <xsl:if test="$code=AddressType/text() and TypeCode/text() != 'TRI'">
              <xsl:call-template name="RFF">
                <xsl:with-param name="suffix" select="'3'"/>
                <xsl:with-param name="code" select="'GN'"/>
                <xsl:with-param name="ref" select="Value/text()"/>
                <xsl:with-param name="regulatingCountry" select="RegulatingCountry/text()"/>
              </xsl:call-template>
            </xsl:if>
          </xsl:for-each>
        </xsl:if>
      </xsl:element>
    </xsl:if>
  </xsl:template>

  <xsl:template name="NAD-Org-Select">
    <xsl:param name="org1"/>
    <xsl:param name="org2"/>
    <xsl:choose>
      <xsl:when test="concat($org1/s0:CompanyName/text(), $org1/s0:Address1/text())!=''">
        <xsl:copy-of select="$org1"/>
      </xsl:when>
      <xsl:otherwise>
        <xsl:copy-of select="$org2"/>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="NAD-Org">
    <xsl:param name="code"/>
    <xsl:param name="org"/>
    <xsl:param name="inttraId"/>
    <xsl:param name="countryOfIssue"/>
    <xsl:param name="suffix" select="''"/>
    <xsl:param name="portOfDestination" select="''"/>

    <xsl:if test="$org">
      <xsl:variable name="countryCode">
        <xsl:choose>
          <xsl:when test="$org/s0:Country/s0:Name/text()!=''">
            <xsl:value-of select="$org/s0:Country/s0:Code/text()"/>
          </xsl:when>
          <xsl:otherwise></xsl:otherwise>
        </xsl:choose>
      </xsl:variable>

      <xsl:call-template name="NAD">
        <xsl:with-param name="code" select="$code"/>
        <xsl:with-param name="id">
          <xsl:choose>
            <xsl:when test="$code='CA'">
              <xsl:value-of select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION' , 'SHIPPING_INSTRUCTION' , 'OCM System Configuration' , 'SCAC' , 'Output Code', $RecipientID,
                          $org/s0:RegistrationNumberCollection/s0:RegistrationNumber[s0:Type/s0:Code/text()='CCC' and s0:CountryOfIssue/s0:Code/text()='US']/s0:Value/text())"/>
            </xsl:when>
            <xsl:when test="$code='FW' or $code='HI' or $code='CZ'">
              <xsl:value-of select="$inttraId"/>
            </xsl:when>
            <xsl:when test="$suffix != '' and ($code = 'UC' or $code = 'BY' or $code = 'ZZZ') and ICS2Helper:IsICS2Port($portOfDestination)">
              <xsl:value-of select="$org/s0:RegistrationNumberCollection/s0:RegistrationNumber[s0:Type/s0:Code/text()='EOR']/s0:Value/text()"/>
            </xsl:when>
          </xsl:choose>
        </xsl:with-param>
        <xsl:with-param name="name" select="$org/s0:CompanyName/text()"/>
        <xsl:with-param name="street1" select="$org/s0:Address1/text()"/>
        <xsl:with-param name="street2" select="$org/s0:Address2/text()"/>
        <xsl:with-param name="city" select="$org/s0:City/text()"/>
        <xsl:with-param name="state" select="$org/s0:State/text()"/>
        <xsl:with-param name="postcode" select="$org/s0:Postcode/text()"/>
        <xsl:with-param name="country" select="$org/s0:Country/s0:Name/text()"/>
        <xsl:with-param name="countrycode" select="$countryCode"/>
        <xsl:with-param name="contact" select="$org/s0:Contact/text()"/>
        <xsl:with-param name="phone" select="$org/s0:Phone/text()"/>
        <xsl:with-param name="email" select="$org/s0:Email/text()"/>
        <xsl:with-param name="fax" select="$org/s0:Fax/text()"/>
        <xsl:with-param name="suffix" select="$suffix"/>
        <xsl:with-param name="registrationNumberCollection" select="$org/s0:RegistrationNumberCollection"/>
        <xsl:with-param name="portOfDestination" select="$portOfDestination"/>
      </xsl:call-template>
    </xsl:if>
  </xsl:template>

  <xsl:template name="GIDLoop">
    <xsl:param name="groupingMethod"/>
    <xsl:param name="segmentPackingLineCollection"/>
    <xsl:param name="unitOfWeight"/>
    <xsl:param name="unitOfVolume"/>
    <xsl:param name="dischargePortPrefix"/>
    <xsl:param name="loadPortPrefix"/>
    <xsl:param name="numberFormat"/>

    <xsl:if test="$segmentPackingLineCollection/s0:PackingLine">
      <xsl:choose>
        <xsl:when test="$groupingMethod != ''">
          <xsl:call-template name="GIDLoopByGroupingMethod">
            <xsl:with-param name="groupingMethod" select="$groupingMethod"/>
            <xsl:with-param name="packingLineCollection" select="$segmentPackingLineCollection" />
            <xsl:with-param name="unitOfWeight" select="$unitOfWeight"/>
            <xsl:with-param name="unitOfVolume" select="$unitOfVolume"/>
            <xsl:with-param name="dischargePortPrefix" select="$dischargePortPrefix"/>
            <xsl:with-param name="loadPortPrefix" select="$loadPortPrefix"/>
          </xsl:call-template>
        </xsl:when>
        <xsl:otherwise>
          <xsl:variable name="isSummary"  select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION' , 'SHIPPING_INSTRUCTION' , 'OCM System Configuration', 'Cargo Details Format', 'Is Summary', $SenderID, $RecipientID)" />

          <xsl:choose>
            <xsl:when test="StringHelper:ToUpper($isSummary)='TRUE'">
              <xsl:call-template name="GIDLoopSummary">
                <xsl:with-param name="packingLineCollection" select="$segmentPackingLineCollection"/>
                <xsl:with-param name="unitOfWeight" select="$unitOfWeight"/>
                <xsl:with-param name="unitOfVolume" select="$unitOfVolume"/>
                <xsl:with-param name="dischargePortPrefix" select="$dischargePortPrefix"/>
                <xsl:with-param name="loadPortPrefix" select="$loadPortPrefix"/>
                <xsl:with-param name="numberFormat" select="$numberFormat"/>
              </xsl:call-template>
            </xsl:when>
            <xsl:otherwise>
              <xsl:call-template name="GIDLoopDetails">
                <xsl:with-param name="packingLineCollection" select="$segmentPackingLineCollection"/>
                <xsl:with-param name="unitOfWeight" select="$unitOfWeight"/>
                <xsl:with-param name="unitOfVolume" select="$unitOfVolume"/>
                <xsl:with-param name="dischargePortPrefix" select="$dischargePortPrefix"/>
                <xsl:with-param name="loadPortPrefix" select="$loadPortPrefix"/>
                <xsl:with-param name="numberFormat" select="$numberFormat"/>
              </xsl:call-template>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:if>
  </xsl:template>

  <xsl:template name="GIDLoopByGroupingMethod">
    <xsl:param name="groupingMethod"/>
    <xsl:param name="packingLineCollection"/>
    <xsl:param name="unitOfWeight"/>
    <xsl:param name="unitOfVolume"/>
    <xsl:param name="dischargePortPrefix"/>
    <xsl:param name="loadPortPrefix"/>

    <xsl:for-each select="$packingLineCollection/s0:PackingLine">
      <xsl:variable name="subShipmentNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='SubShipmentNumber']/s0:Value/text()" />
      <xsl:variable name="matchedSubShipment" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]" />

      <ns0:GIDLoop1>
        <xsl:variable name="resetFTXCounter" select="userCSharp:ResetFTXCounter()" />

        <xsl:call-template name="GID">
          <xsl:with-param name="packingLine" select="."/>
        </xsl:call-template>

        <xsl:if test="StringHelper:ToUpper($ServiceProvider) != 'YANGMING'">
          <xsl:if test="$shipmentType != 'DRT' and $ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
            <xsl:variable name="portOfOriginCode" select="$matchedSubShipment/s0:PortOfOrigin/s0:Code/text()"/>
            <xsl:if test="$portOfOriginCode != ''">
              <xsl:call-template name="LOC">
                <xsl:with-param name="code" select="'198'"/>
                <xsl:with-param name="unloco" select="$portOfOriginCode"/>
                <xsl:with-param name="name" select="$matchedSubShipment/s0:PortOfOrigin/s0:Name/text()"/>
                <xsl:with-param name="suffix" select="'_10'"/>
              </xsl:call-template>
            </xsl:if>
            <xsl:variable name="portOfDestinationCode" select="$matchedSubShipment/s0:PortOfDestination/s0:Code/text()"/>
            <xsl:if test="$portOfDestinationCode != ''">
              <xsl:call-template name="LOC">
                <xsl:with-param name="code" select="'83'"/>
                <xsl:with-param name="unloco" select="$portOfDestinationCode"/>
                <xsl:with-param name="name" select="$matchedSubShipment/s0:PortOfDestination/s0:Name/text()"/>
                <xsl:with-param name="suffix" select="'_10'"/>
              </xsl:call-template>
            </xsl:if>
          </xsl:if>
        </xsl:if>

        <xsl:call-template name="GID-PIA">
          <xsl:with-param name="packingLine" select="."/>
        </xsl:call-template>

        <xsl:if test="$shipmentType != 'DRT' and $ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
          <xsl:variable name="paymentMethod">
            <xsl:call-template name="GetValueOrDefault">
              <xsl:with-param name="value1" select="$matchedSubShipment/s0:PaymentHandlingInstructionCollection/s0:PaymentHandlingInstruction[s0:Category/s0:Code='HPT']/s0:PaymentMethod/s0:Code/text()"/>
              <xsl:with-param name="value2" select="'D'"/>
            </xsl:call-template>
          </xsl:variable>
          <xsl:call-template name="FTX-CCI-FixedTextValues">
            <xsl:with-param name="type" select="'CCI'"/>
            <xsl:with-param name="code" select="'HBL'"/>
            <xsl:with-param name="suffix" select="'_5'"/>
            <xsl:with-param name="text1" select="$paymentMethod" />
            <xsl:with-param name="text2" select="translate($matchedSubShipment/s0:AddInfoCollection/s0:AddInfo[s0:Key/text() = 'CountriesOfRouting']/s0:Value/text(), '|', '-')"/>
            <xsl:with-param name="text3">
              <xsl:variable name="consigneeCompanyName" select="$matchedSubShipment/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text() = 'ConsigneeDocumentaryAddress']/s0:CompanyName/text()"/>
              <xsl:choose>
                <xsl:when test="not($consigneeCompanyName) or $consigneeCompanyName = '' or userCSharp:IsToOrder($consigneeCompanyName)">TOS</xsl:when>
                <xsl:otherwise></xsl:otherwise>
              </xsl:choose>
            </xsl:with-param>
          </xsl:call-template>
        </xsl:if>

        <xsl:if test="$shipmentType != 'DRT' and $ICS2FilingType = 'Carrier'">
          <xsl:call-template name="FTX-CCI-FixedTextValues">
            <xsl:with-param name="type" select="'CCI'"/>
            <xsl:with-param name="code" select="'AAA'"/>
            <xsl:with-param name="suffix" select="'_5'"/>
            <xsl:with-param name="text1">
              <xsl:variable name="value">
                <xsl:call-template name="GetValueOrDefault">
                  <xsl:with-param name="value1" select="StringHelper:ReplaceCRLFText(s0:DetailedDescription/text())"/>
                  <xsl:with-param name="value2" select="StringHelper:ReplaceCRLFText(s0:GoodsDescription/text())"/>
                </xsl:call-template>
              </xsl:variable>
              <xsl:value-of select="StringHelper:SubstringSafe($value, 0, 512)"/>
            </xsl:with-param>
            <xsl:with-param name="text2">
              <xsl:variable name="value" select="StringHelper:ReplaceCRLFText(s0:MarksAndNos/text())"/>
              <xsl:value-of select="StringHelper:SubstringSafe($value, 0, 512)"/>
            </xsl:with-param>
          </xsl:call-template>
        </xsl:if>

        <xsl:variable name="goodsDescription">
          <xsl:call-template name="GetValueOrDefault">
            <xsl:with-param name="value1" select="s0:DetailedDescription/text()"/>
            <xsl:with-param name="defaultValue" select="s0:GoodsDescription/text()"/>
          </xsl:call-template>
        </xsl:variable>
        <xsl:variable name="goodsDescriptionAndCTK">
          <xsl:call-template name="GetGoodsDescriptionAndCTK">
            <xsl:with-param name="goodsDescription" select="$goodsDescription"/>
            <xsl:with-param name="CTKNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='CTK']/s0:Value/text()"/>
          </xsl:call-template>
        </xsl:variable>
        <xsl:if test="$goodsDescriptionAndCTK!=''">
          <xsl:call-template name="FTX-AAA-Code">
            <xsl:with-param name="text" select="$goodsDescriptionAndCTK"/>
          </xsl:call-template>
        </xsl:if>

        <xsl:call-template name="GID-NAD">
          <xsl:with-param name="packingLine" select="."/>
        </xsl:call-template>

        <xsl:call-template name="GID-MEA">
          <xsl:with-param name="packingLine" select="."/>
          <xsl:with-param name="unitOfWeight" select="$unitOfWeight"/>
          <xsl:with-param name="unitOfVolume" select="$unitOfVolume"/>
        </xsl:call-template>

        <xsl:call-template name="GID-RFF">
          <xsl:with-param name="packingLine" select="."/>
          <xsl:with-param name="dischargePortPrefix" select="$dischargePortPrefix"/>
          <xsl:with-param name="loadPortPrefix" select="$loadPortPrefix"/>
        </xsl:call-template>

        <xsl:call-template name="GID-PCI">
          <xsl:with-param name="text" select="s0:MarksAndNos/text()"/>
        </xsl:call-template>

        <xsl:call-template name="GID-SGP">
          <xsl:with-param name="packingLines" select="s0:PackingLineCollection/s0:PackingLine"/>
          <xsl:with-param name="unitOfWeight" select="$unitOfWeight"/>
          <xsl:with-param name="unitOfVolume" select="$unitOfVolume"/>
        </xsl:call-template>

        <xsl:call-template name="DGS-GroupingMethod">
          <xsl:with-param name="packingLines" select="s0:PackingLineCollection/s0:PackingLine"/>
          <xsl:with-param name="groupingMethod" select="$groupingMethod"/>
        </xsl:call-template>
      </ns0:GIDLoop1>
    </xsl:for-each>
  </xsl:template>

  <xsl:template name="DGS-GroupingMethod">
    <xsl:param name="packingLines"/>
    <xsl:param name ="groupingMethod"/>

    <xsl:for-each select="$packingLines">
      <xsl:variable name="clearList" select="ListHelper:ClearList()" />
      <xsl:for-each select="s0:UNDGCollection/s0:UNDG[s0:UNDGCode/text()!='']">
        <xsl:variable name="dgsKey" select="concat(s0:IMOClass,'+',s0:SubLabel1,'+',s0:UNDGCode,'+',s0:FlashPoint,'+',s0:PackingGroup,'+',s0:ProperShippingName,'+', s0:TechicalName,'+',  s0:MarinePollutant, '+', s0:Contact/s0:FullName, '+', s0:Contact/s0:Phone, '+', s0:PackType/s0:Code, '+', s0:PackedInLimitedQuantity)" />
        <xsl:variable name="shouldCreateDGS" select="ListHelper:ShouldCreateItem('DGS+', $dgsKey)" />
        <xsl:if test="$shouldCreateDGS">
          <xsl:call-template name="DGSLoop-Core">
            <xsl:with-param name="undgNode" select="."/>
          </xsl:call-template>
        </xsl:if>
      </xsl:for-each>
    </xsl:for-each>
  </xsl:template>

  <xsl:template name="GIDLoopSummary">
    <xsl:param name="packingLineCollection" />
    <xsl:param name="unitOfWeight"/>
    <xsl:param name="unitOfVolume"/>
    <xsl:param name="dischargePortPrefix"/>
    <xsl:param name="loadPortPrefix"/>
    <xsl:param name="numberFormat"/>

    <xsl:variable name="packingLineCollectionGID_rtf">
      <xsl:for-each select="$packingLineCollection/s0:PackingLine">
        <s0:PackingLine>
          <xsl:copy-of select="*"/>
          <s0:DetailedGoodsDescription>
            <xsl:value-of select="s0:DetailedDescription/text()"/>
          </s0:DetailedGoodsDescription>
          <s0:GidKey>
            <xsl:variable name="subShipmentNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='SubShipmentNumber']/s0:Value/text()" />
            <xsl:variable name="subShipmentWaybillNumber" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:WayBillNumber/text()" />

            <xsl:value-of select="concat(s0:DetailedDescription/text(), '+', s0:PackType/s0:Code/text(), '+', s0:MarksAndNos/text(), '+', $subShipmentWaybillNumber)"/>
          </s0:GidKey>
        </s0:PackingLine>
      </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="packingLineCollectionGID" select="msxsl:node-set($packingLineCollectionGID_rtf)"/>

    <xsl:for-each select="$packingLineCollectionGID/s0:PackingLine">

      <xsl:variable name="gidKey" select="s0:GidKey"/>
      <xsl:variable name="shouldCreateGID" select="ListHelper:ShouldCreateItem('GID+', $gidKey)" />

      <xsl:if test="$shouldCreateGID='true'">
        <xsl:variable name="matchedPackingLines" select="$packingLineCollectionGID/s0:PackingLine[s0:GidKey=$gidKey]"/>

        <xsl:variable name="totalPackQty" select="sum($matchedPackingLines/s0:PackQty/text())" />
        <xsl:variable name="totalWeight" select="sum($matchedPackingLines/s0:Weight/text())" />
        <xsl:variable name="totalVolume" select="sum($matchedPackingLines/s0:Volume/text())" />

        <ns0:GIDLoop1>
          <xsl:variable name="resetFTXCounter" select="userCSharp:ResetFTXCounter()" />

          <ns0:GID>
            <GID01>
              <xsl:value-of select="userCSharp:GIDCounter()"/>
            </GID01>
            <ns0:C213>
              <C21301>
                <xsl:value-of select="$totalPackQty"/>
              </C21301>
              <C21302>
                <xsl:variable name="packageTypeCode" select="s0:PackType/s0:Code/text()"/>

                <xsl:variable name="packageTypeMappingV1" select="CodeMapper:GetRecipientCode($CarrierName , $CarrierName , $combineRecipientMappingNamev1, 'Package Type' , $combineRecipientCodeField, $packageTypeCode)"/>
                <xsl:choose>
                  <xsl:when test="$packageTypeMappingV1!=''">
                    <xsl:value-of select="$packageTypeMappingV1"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:value-of select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION' , 'SHIPPING_INSTRUCTION' , 'OCM System Configuration', 'Package Type ISO' , 'Package Type', $packageTypeCode)"/>
                  </xsl:otherwise>
                </xsl:choose>
              </C21302>
              <C21304>6</C21304>
              <C21305>
                <xsl:value-of select="s0:PackType/s0:Description/text()" />
              </C21305>
            </ns0:C213>
          </ns0:GID>

          <xsl:if test="StringHelper:ToUpper($ServiceProvider) != 'YANGMING'">
            <xsl:for-each select="$matchedPackingLines">
              <xsl:variable name="subShipmentNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='SubShipmentNumber']/s0:Value/text()" />
              <xsl:variable name="matchedSubShipment" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]" />

              <xsl:variable name="portOfOriginCode" select="$matchedSubShipment/s0:PortOfOrigin/s0:Code/text()"/>
              <xsl:variable name="portOfOriginName" select="$matchedSubShipment/s0:PortOfOrigin/s0:Name/text()"/>
              <xsl:variable name="portOfDestinationCode" select="$matchedSubShipment/s0:PortOfDestination/s0:Code/text()"/>
              <xsl:variable name="portOfDestinationName" select="$matchedSubShipment/s0:PortOfDestination/s0:Name/text()"/>
              <xsl:variable name="shouldCreateGID-LOC-198" select="ListHelper:ShouldCreateItem(concat('GID_LOC_198', $gidKey), concat($portOfOriginCode, '-', $portOfOriginName))" />
              <xsl:variable name="shouldCreateGID-LOC-83" select="ListHelper:ShouldCreateItem(concat('GID_LOC_83', $gidKey), concat($portOfDestinationCode, '-', $portOfDestinationName))" />
              <xsl:if test="$shipmentType != 'DRT' and $ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
                <xsl:if test="$portOfOriginCode != '' and $shouldCreateGID-LOC-198 = 'true'">
                  <xsl:call-template name="LOC">
                    <xsl:with-param name="code" select="'198'"/>
                    <xsl:with-param name="unloco" select="$portOfOriginCode"/>
                    <xsl:with-param name="name" select="$portOfOriginName"/>
                    <xsl:with-param name="suffix" select="'_10'"/>
                  </xsl:call-template>
                </xsl:if>
                <xsl:if test="$portOfDestinationCode !='' and  $shouldCreateGID-LOC-83 = 'true'">
                  <xsl:call-template name="LOC">
                    <xsl:with-param name="code" select="'83'"/>
                    <xsl:with-param name="unloco" select="$portOfDestinationCode"/>
                    <xsl:with-param name="name" select="$portOfDestinationName"/>
                    <xsl:with-param name="suffix" select="'_10'"/>
                  </xsl:call-template>
                </xsl:if>
              </xsl:if>
            </xsl:for-each>
          </xsl:if>

          <xsl:for-each select="$matchedPackingLines">
            <xsl:call-template name="GID-PIA">
              <xsl:with-param name="packingLine" select="."/>
              <xsl:with-param name="isSummary" select="'TRUE'"/>
              <xsl:with-param name="key" select="$gidKey"/>
            </xsl:call-template>
          </xsl:for-each>

          <xsl:if test="$shipmentType != 'DRT' and $ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
            <xsl:for-each select="$matchedPackingLines">
              <xsl:variable name="subShipmentNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='SubShipmentNumber']/s0:Value/text()" />
              <xsl:variable name="matchedSubShipment" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]" />
              <xsl:variable name="countriesOfRouting" select="translate($matchedSubShipment/s0:AddInfoCollection/s0:AddInfo[s0:Key/text() = 'CountriesOfRouting']/s0:Value/text(), '|', '-')"/>
              <xsl:variable name="consigneeCompanyName" select="$matchedSubShipment/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text() = 'ConsigneeDocumentaryAddress']/s0:CompanyName/text()"/>

              <xsl:variable name="cciHBL_text3">
                <xsl:choose>
                  <xsl:when test="not($consigneeCompanyName) or $consigneeCompanyName = '' or userCSharp:IsToOrder($consigneeCompanyName)">TOS</xsl:when>
                  <xsl:otherwise></xsl:otherwise>
                </xsl:choose>
              </xsl:variable>

              <xsl:variable name="shouldCreateFTX-CCI-HBL" select="ListHelper:ShouldCreateItem(concat('FTX_CCI_5_HBL', $gidKey), concat($countriesOfRouting, '-', $cciHBL_text3))" />
              <xsl:if test="$shouldCreateFTX-CCI-HBL = 'true'">
                <xsl:call-template name="FTX-CCI-FixedTextValues">
                  <xsl:with-param name="type" select="'CCI'"/>
                  <xsl:with-param name="code" select="'HBL'"/>
                  <xsl:with-param name="suffix" select="'_5'"/>
                  <xsl:with-param name="text1" select="'NP'"/>
                  <xsl:with-param name="text2" select="$countriesOfRouting"/>
                  <xsl:with-param name="text3" select="$cciHBL_text3"/>
                </xsl:call-template>
              </xsl:if>
            </xsl:for-each>
          </xsl:if>

          <xsl:if test="$shipmentType != 'DRT' and $ICS2FilingType = 'Carrier'">
            <xsl:call-template name="FTX-CCI-FixedTextValues">
              <xsl:with-param name="type" select="'CCI'"/>
              <xsl:with-param name="code" select="'AAA'"/>
              <xsl:with-param name="suffix" select="'_5'"/>
              <xsl:with-param name="text1">
                <xsl:variable name="value">
                  <xsl:call-template name="GetValueOrDefault">
                    <xsl:with-param name="value1" select="StringHelper:ReplaceCRLFText(s0:DetailedDescription/text())"/>
                    <xsl:with-param name="value2" select="StringHelper:ReplaceCRLFText(s0:GoodsDescription/text())"/>
                  </xsl:call-template>
                </xsl:variable>
                <xsl:value-of select="StringHelper:SubstringSafe($value, 0, 512)"/>
              </xsl:with-param>
              <xsl:with-param name="text2">
                <xsl:variable name="value" select="StringHelper:ReplaceCRLFText(s0:MarksAndNos/text())"/>
                <xsl:value-of select="StringHelper:SubstringSafe($value, 0, 512)"/>
              </xsl:with-param>
            </xsl:call-template>
          </xsl:if>

          <xsl:variable name="goodsDescriptionAndCTK">
            <xsl:call-template name="GetGoodsDescriptionAndCTK">
              <xsl:with-param name="goodsDescription" select="s0:DetailedGoodsDescription/text()"/>
              <xsl:with-param name="CTKNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='CTK']/s0:Value/text()"/>
            </xsl:call-template>
          </xsl:variable>
          <xsl:if test="$goodsDescriptionAndCTK!=''">
            <xsl:call-template name="FTX-AAA-Code">
              <xsl:with-param name="text" select="$goodsDescriptionAndCTK"/>
            </xsl:call-template>
          </xsl:if>

          <xsl:if test="$ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
            <xsl:for-each select="$matchedPackingLines">
              <xsl:variable name="subShipmentNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='SubShipmentNumber']/s0:Value/text()" />
              <xsl:variable name="organizationAddressCollection" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:OrganizationAddressCollection" />

              <xsl:if test="$subShipmentNumber != '' and $organizationAddressCollection">
                <xsl:variable name="consignorDocumentaryAddress" select="$organizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsignorDocumentaryAddress']"/>
                <xsl:variable name="consigneeDocumentaryAddress" select="$organizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsigneeDocumentaryAddress']"/>
                <xsl:variable name="notifyParty" select="$organizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='NotifyParty']"/>
                <xsl:variable name="supplierDocumentaryAddress">
                  <xsl:call-template name="NAD-Org-Select">
                    <xsl:with-param name="org1" select="$organizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='SupplierDocumentaryAddress']"/>
                    <xsl:with-param name="org2" select="$consignorDocumentaryAddress"/>
                  </xsl:call-template>
                </xsl:variable>
                <xsl:variable name="buyerDocumentaryAddress">
                  <xsl:call-template name="NAD-Org-Select">
                    <xsl:with-param name="org1" select="$organizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='BuyerDocumentaryAddress']"/>
                    <xsl:with-param name="org2" select="$consigneeDocumentaryAddress"/>
                  </xsl:call-template>
                </xsl:variable>

                <xsl:variable name="shouldCreateNAD_OS" select="ListHelper:ShouldCreateItem(concat('NAD_OS_', $gidKey), concat('OS_', $consignorDocumentaryAddress/s0:CompanyName/text()))" />
                <xsl:if test="$shouldCreateNAD_OS = 'true'">
                  <xsl:call-template name="NAD-Org">
                    <xsl:with-param name="code" select="'OS'"/>
                    <xsl:with-param name="org" select="$consignorDocumentaryAddress"/>
                    <xsl:with-param name="suffix" select="'_2'"/>
                  </xsl:call-template>
                </xsl:if>
                <xsl:variable name="shouldCreateNAD_UC" select="ListHelper:ShouldCreateItem(concat('NAD_UC_', $gidKey), concat('UC_', $consigneeDocumentaryAddress/s0:CompanyName/text()))" />
                <xsl:if test="$shouldCreateNAD_UC = 'true'">
                  <xsl:call-template name="NAD-Org">
                    <xsl:with-param name="code" select="'UC'"/>
                    <xsl:with-param name="org" select="$consigneeDocumentaryAddress"/>
                    <xsl:with-param name="suffix" select="'_2'"/>
                    <xsl:with-param name="portOfDestination" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:PortOfDestination/s0:Code/text()"/>
                  </xsl:call-template>
                </xsl:if>
                <xsl:variable name="shouldCreateNAD_SE" select="ListHelper:ShouldCreateItem(concat('NAD_SE_', $gidKey), concat('SE_', msxsl:node-set($supplierDocumentaryAddress)/s0:OrganizationAddress/s0:CompanyName/text()))" />
                <xsl:if test="$shouldCreateNAD_SE = 'true'">
                  <xsl:call-template name="NAD-Org">
                    <xsl:with-param name="code" select="'SE'"/>
                    <xsl:with-param name="org" select="msxsl:node-set($supplierDocumentaryAddress)/s0:OrganizationAddress"/>
                    <xsl:with-param name="suffix" select="'_2'"/>
                  </xsl:call-template>
                </xsl:if>
                <xsl:variable name="shouldCreateNAD_BY" select="ListHelper:ShouldCreateItem(concat('NAD_BY_', $gidKey), concat('BY_', msxsl:node-set($buyerDocumentaryAddress)/s0:OrganizationAddress/s0:CompanyName/text()))" />
                <xsl:if test="$shouldCreateNAD_BY = 'true'">
                  <xsl:call-template name="NAD-Org">
                    <xsl:with-param name="code" select="'BY'"/>
                    <xsl:with-param name="org" select="msxsl:node-set($buyerDocumentaryAddress)/s0:OrganizationAddress"/>
                    <xsl:with-param name="suffix" select="'_2'"/>
                    <xsl:with-param name="portOfDestination" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:PortOfDestination/s0:Code/text()"/>
                  </xsl:call-template>
                </xsl:if>
                <xsl:variable name="shouldCreateNAD_ZZZ" select="ListHelper:ShouldCreateItem(concat('NAD_ZZZ_', $gidKey), concat('ZZZ_', $notifyParty/s0:CompanyName/text()))" />
                <xsl:if test="$shouldCreateNAD_ZZZ = 'true'">
                  <xsl:call-template name="NAD-Org">
                    <xsl:with-param name="code" select="'ZZZ'"/>
                    <xsl:with-param name="org" select="$notifyParty"/>
                    <xsl:with-param name="suffix" select="'_2'"/>
                    <xsl:with-param name="portOfDestination" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:PortOfDestination/s0:Code/text()"/>
                  </xsl:call-template>
                </xsl:if>
              </xsl:if>
            </xsl:for-each>
          </xsl:if>

          <ns0:MEALoop1>
            <xsl:call-template name="MEA">
              <xsl:with-param name="code" select="'AAE'"/>
              <xsl:with-param name="type" select="'WT'"/>
              <xsl:with-param name="unit" select="$unitOfWeight"/>
              <xsl:with-param name="value" select="StringMapper:FormatDecimal($totalWeight,'0.###',false())"/>
              <xsl:with-param name="suffix" select="''"/>
            </xsl:call-template>
          </ns0:MEALoop1>
          <ns0:MEALoop1>
            <xsl:call-template name="MEA">
              <xsl:with-param name="code" select="'AAE'"/>
              <xsl:with-param name="type" select="'AAW'"/>
              <xsl:with-param name="unit" select="$unitOfVolume"/>
              <xsl:with-param name="value" select="StringMapper:FormatDecimal($totalVolume,'0.####',false())"/>
              <xsl:with-param name="suffix" select="''"/>
            </xsl:call-template>
          </ns0:MEALoop1>

          <xsl:variable name="clearSG22RFF" select="ListHelperClear:ClearList()" />

          <xsl:if test="$dischargePortPrefix='BR' or $loadPortPrefix='BR'">
            <xsl:for-each select="$matchedPackingLines">
              <xsl:variable name="harmonisedCode" select="StringHelper:StringReplace(s0:HarmonisedCode/text(), '.', '')"/>
              <xsl:if test="$harmonisedCode!='' and ListHelperClear:ShouldCreateItem('SG22RFF+', concat('ABT+', $harmonisedCode), 9)">
                <xsl:call-template name="RFF">
                  <xsl:with-param name="suffix" select="'4'"/>
                  <xsl:with-param name="code" select="'ABT'"/>
                  <xsl:with-param name="ref" select="$harmonisedCode"/>
                </xsl:call-template>
              </xsl:if>
            </xsl:for-each>
            <xsl:for-each select="$matchedPackingLines/s0:ClassificationCollection/s0:Classification[s0:Country/s0:Code/text()='BR']">
              <xsl:variable name="classificationCode" select="StringHelper:StringReplace(s0:Code/text(), '.', '')"/>
              <xsl:if test="$classificationCode!='' and ListHelperClear:ShouldCreateItem('SG22RFF+', concat('ABT+', $classificationCode), 9)">
                <xsl:call-template name="RFF">
                  <xsl:with-param name="suffix" select="'4'"/>
                  <xsl:with-param name="code" select="'ABT'"/>
                  <xsl:with-param name="ref" select="$classificationCode"/>
                </xsl:call-template>
              </xsl:if>
            </xsl:for-each>
          </xsl:if>

          <xsl:if test="$ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
            <xsl:for-each select="$matchedPackingLines">
              <xsl:variable name="subShipmentNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='SubShipmentNumber']/s0:Value/text()" />
              <xsl:variable name="subShipmentWaybillNumber" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:WayBillNumber/text()" />
              <xsl:if test="$subShipmentWaybillNumber!=''
                      and ListHelper:ShouldCreateItem(concat('RFFBH+', $gidKey, '+'), $subShipmentWaybillNumber)
                      and ListHelperClear:ShouldCreateItem('SG22RFF+', concat('BH+', $subShipmentWaybillNumber), 9)">
                <xsl:call-template name="RFF">
                  <xsl:with-param name="suffix" select="'4'"/>
                  <xsl:with-param name="code" select="'BH'"/>
                  <xsl:with-param name="ref" select="$subShipmentWaybillNumber"/>
                </xsl:call-template>
              </xsl:if>
            </xsl:for-each>
          </xsl:if>

          <xsl:for-each select="$matchedPackingLines">
            <xsl:variable name="ITNNumber">
              <xsl:variable name="itn" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='ITN']/s0:Value/text()" />
              <xsl:variable name="shouldCreateITN" select="ListHelper:ShouldCreateItem(concat('ITN+', $gidKey), $itn)" />
              <xsl:if test="$shouldCreateITN and $itn!=''">
                <xsl:value-of select="$itn" />
              </xsl:if>
            </xsl:variable>
            <xsl:variable name="ExportReferenceNumber">
              <xsl:variable name="exportReference" select="s0:ExportReferenceNumber/text()" />
              <xsl:variable name="shouldCreateExportReference" select="ListHelper:ShouldCreateItem(concat('EXR+', $gidKey), $exportReference)" />
              <xsl:if test="$shouldCreateExportReference and $exportReference!=''">
                <xsl:value-of select="$exportReference" />
              </xsl:if>
            </xsl:variable>
            <xsl:variable name="DUENumber">
              <xsl:variable name="due" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='DUE']/s0:Value/text()" />
              <xsl:variable name="shouldCreateDUE" select="ListHelper:ShouldCreateItem(concat('DUE+', $gidKey), $due)" />
              <xsl:if test="$shouldCreateDUE and $due!=''">
                <xsl:value-of select="$due" />
              </xsl:if>
            </xsl:variable>
            <xsl:variable name="CTNNumber">
              <xsl:variable name="ctn" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='CTN']/s0:Value/text()" />
              <xsl:variable name="shouldCreateCTN" select="ListHelper:ShouldCreateItem(concat('CTN+', $gidKey), $ctn)" />
              <xsl:if test="$shouldCreateCTN and $ctn!=''">
                <xsl:value-of select="$ctn" />
              </xsl:if>
            </xsl:variable>
            <xsl:variable name="MRNNumber">
              <xsl:variable name="mrn" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='MRN']/s0:Value/text()" />
              <xsl:variable name="shouldCreateMRN" select="ListHelper:ShouldCreateItem(concat('MRN+', $gidKey), $mrn)" />
              <xsl:if test="$shouldCreateMRN and $mrn!=''">
                <xsl:value-of select="$mrn" />
              </xsl:if>
            </xsl:variable>

            <xsl:call-template name="RFF-TN">
              <xsl:with-param name="ITNNumber" select="$ITNNumber" />
              <xsl:with-param name="DUENumber" select="$DUENumber" />
              <xsl:with-param name="exportReferenceNumber" select="$ExportReferenceNumber"/>
              <xsl:with-param name="CTNNumber" select="$CTNNumber" />
              <xsl:with-param name="MRNNumber" select="$MRNNumber" />
            </xsl:call-template>
          </xsl:for-each>

          <xsl:for-each select="$matchedPackingLines/s0:ClassificationCollection/s0:Classification[s0:Type/s0:Code/text() = 'CUS']">
            <xsl:variable name="classificationCode" select="StringHelper:StringReplace(s0:Code/text(), '.', '')"/>
            <xsl:if test="$classificationCode!='' and ListHelperClear:ShouldCreateItem('SG22RFF+', concat('AIY+', $classificationCode), 9)">
              <xsl:call-template name="RFF">
                <xsl:with-param name="suffix" select="'4'"/>
                <xsl:with-param name="code" select="'AIY'"/>
                <xsl:with-param name="ref" select="$classificationCode"/>
              </xsl:call-template>
            </xsl:if>
          </xsl:for-each>

          <xsl:call-template name="GID-PCI">
            <xsl:with-param name="text" select="s0:MarksAndNos/text()"/>
          </xsl:call-template>

          <xsl:for-each select="$matchedPackingLines[s0:ContainerNumber/text()!='']">
            <xsl:variable name="containerNumber" select="s0:ContainerNumber/text()" />
            <xsl:variable name="shouldCreateCTR" select="ListHelper:ShouldCreateItem('CTR+', concat($gidKey, $containerNumber))" />
            <xsl:if test="$shouldCreateCTR">
              <xsl:variable name="totalPackQtyCTR" select="sum($matchedPackingLines[s0:ContainerNumber/text()=$containerNumber]/s0:PackQty/text())" />
              <xsl:variable name="totalWeightCTR" select="sum($matchedPackingLines[s0:ContainerNumber/text()=$containerNumber]/s0:Weight/text())" />
              <xsl:variable name="totalVolumeCTR" select="sum($matchedPackingLines[s0:ContainerNumber/text()=$containerNumber]/s0:Volume/text())" />
              <ns0:SGPLoop1>
                <ns0:SGP>
                  <ns0:C237>
                    <C23701>
                      <xsl:value-of select="s0:ContainerNumber/text()"/>
                    </C23701>
                  </ns0:C237>
                  <SGP02>
                    <xsl:value-of select="$totalPackQtyCTR"/>
                  </SGP02>
                </ns0:SGP>
                <ns0:MEALoop3>
                  <xsl:call-template name="MEA">
                    <xsl:with-param name="code" select="'AAE'"/>
                    <xsl:with-param name="type" select="'WT'"/>
                    <xsl:with-param name="unit" select="$unitOfWeight"/>
                    <xsl:with-param name="value" select="StringMapper:FormatDecimal($totalWeightCTR,'0.###',false())"/>
                    <xsl:with-param name="suffix" select="'_3'"/>
                  </xsl:call-template>
                </ns0:MEALoop3>
                <ns0:MEALoop3>
                  <xsl:call-template name="MEA">
                    <xsl:with-param name="code" select="'AAE'"/>
                    <xsl:with-param name="type" select="'AAW'"/>
                    <xsl:with-param name="unit" select="$unitOfVolume"/>
                    <xsl:with-param name="value" select="StringMapper:FormatDecimal($totalVolumeCTR,'0.####',false())"/>
                    <xsl:with-param name="suffix" select="'_3'"/>
                  </xsl:call-template>
                </ns0:MEALoop3>
              </ns0:SGPLoop1>
            </xsl:if>
          </xsl:for-each>

          <xsl:for-each select="$matchedPackingLines/s0:UNDGCollection/s0:UNDG[s0:UNDGCode/text()!='']">
            <xsl:variable name="dgsKey" select="concat(s0:IMOClass,'+',s0:SubLabel1,'+',s0:UNDGCode,'+',s0:FlashPoint,'+',s0:PackingGroup,'+',s0:ProperShippingName,'+', s0:TechicalName,'+',  s0:MarinePollutant, '+', s0:Contact/s0:FullName, '+', s0:Contact/s0:Phone, '+', s0:PackType/s0:Code, '+', s0:PackedInLimitedQuantity)" />
            <xsl:variable name="shouldCreateDGS" select="ListHelper:ShouldCreateItem(concat('DGS+', $gidKey, '+'), concat($gidKey, $dgsKey), 99)" />
            <xsl:if test="$shouldCreateDGS">
              <xsl:call-template name="DGSLoop-Core">
                <xsl:with-param name="undgNode" select="."/>
                <xsl:with-param name="numberFormat" select="$numberFormat"/>
              </xsl:call-template>
            </xsl:if>
          </xsl:for-each>
        </ns0:GIDLoop1>

      </xsl:if>
    </xsl:for-each>
  </xsl:template>

  <xsl:template name="GIDLoopDetails">
    <xsl:param name="packingLineCollection"/>
    <xsl:param name="unitOfWeight"/>
    <xsl:param name="unitOfVolume"/>
    <xsl:param name="dischargePortPrefix"/>
    <xsl:param name="loadPortPrefix"/>
    <xsl:param name="numberFormat"/>

    <xsl:for-each select="$packingLineCollection/s0:PackingLine">
      <ns0:GIDLoop1>
        <xsl:variable name="resetFTXCounter" select="userCSharp:ResetFTXCounter()" />

        <xsl:call-template name="GID">
          <xsl:with-param name="packingLine" select="."/>
        </xsl:call-template>

        <xsl:variable name="subShipmentNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='SubShipmentNumber']/s0:Value/text()" />
        <xsl:variable name="matchedSubShipment" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]" />

        <xsl:if test="StringHelper:ToUpper($ServiceProvider) != 'YANGMING'">
          <xsl:if test="$shipmentType != 'DRT' and $ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
            <xsl:variable name="portOfOriginCode" select="$matchedSubShipment/s0:PortOfOrigin/s0:Code/text()"/>
            <xsl:if test="$portOfOriginCode != ''">
              <xsl:call-template name="LOC">
                <xsl:with-param name="code" select="'198'"/>
                <xsl:with-param name="unloco" select="$portOfOriginCode"/>
                <xsl:with-param name="name" select="$matchedSubShipment/s0:PortOfOrigin/s0:Name/text()"/>
                <xsl:with-param name="suffix" select="'_10'"/>
              </xsl:call-template>
            </xsl:if>
            <xsl:variable name="portOfDestinationCode" select="$matchedSubShipment/s0:PortOfDestination/s0:Code/text()"/>
            <xsl:if test="$portOfDestinationCode != ''">
              <xsl:call-template name="LOC">
                <xsl:with-param name="code" select="'83'"/>
                <xsl:with-param name="unloco" select="$portOfDestinationCode"/>
                <xsl:with-param name="name" select="$matchedSubShipment/s0:PortOfDestination/s0:Name/text()"/>
                <xsl:with-param name="suffix" select="'_10'"/>
              </xsl:call-template>
            </xsl:if>
          </xsl:if>
        </xsl:if>

        <xsl:call-template name="GID-PIA">
          <xsl:with-param name="packingLine" select="."/>
        </xsl:call-template>

        <xsl:if test="$shipmentType != 'DRT' and $ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
          <xsl:variable name="consigneeCompanyName" select="$matchedSubShipment/s0:OrganizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text() = 'ConsigneeDocumentaryAddress']/s0:CompanyName/text()"/>
          <xsl:variable name="cciHBL_text3">
            <xsl:choose>
              <xsl:when test="not($consigneeCompanyName) or $consigneeCompanyName = '' or userCSharp:IsToOrder($consigneeCompanyName)">TOS</xsl:when>
              <xsl:otherwise></xsl:otherwise>
            </xsl:choose>
          </xsl:variable>

          <xsl:call-template name="FTX-CCI-FixedTextValues">
            <xsl:with-param name="type" select="'CCI'"/>
            <xsl:with-param name="code" select="'HBL'"/>
            <xsl:with-param name="suffix" select="'_5'"/>
            <xsl:with-param name="text1" select="'NP'"/>
            <xsl:with-param name="text2" select="translate($matchedSubShipment/s0:AddInfoCollection/s0:AddInfo[s0:Key/text() = 'CountriesOfRouting']/s0:Value/text(), '|', '-')"/>
            <xsl:with-param name="text3" select="$cciHBL_text3" />
          </xsl:call-template>
        </xsl:if>

        <xsl:if test="$shipmentType != 'DRT' and $ICS2FilingType = 'Carrier'">
          <xsl:call-template name="FTX-CCI-FixedTextValues">
            <xsl:with-param name="type" select="'CCI'"/>
            <xsl:with-param name="code" select="'AAA'"/>
            <xsl:with-param name="suffix" select="'_5'"/>
            <xsl:with-param name="text1">
              <xsl:variable name="value">
                <xsl:call-template name="GetValueOrDefault">
                  <xsl:with-param name="value1" select="StringHelper:ReplaceCRLFText(s0:DetailedDescription/text())"/>
                  <xsl:with-param name="value2" select="StringHelper:ReplaceCRLFText(s0:GoodsDescription/text())"/>
                </xsl:call-template>
              </xsl:variable>
              <xsl:value-of select="StringHelper:SubstringSafe($value, 0, 512)"/>
            </xsl:with-param>
            <xsl:with-param name="text2">
              <xsl:variable name="value" select="StringHelper:ReplaceCRLFText(s0:MarksAndNos/text())"/>
              <xsl:value-of select="StringHelper:SubstringSafe($value, 0, 512)"/>
            </xsl:with-param>
          </xsl:call-template>
        </xsl:if>

        <xsl:variable name="goodsDescriptionAndCTK">
          <xsl:call-template name="GetGoodsDescriptionAndCTK">
            <xsl:with-param name="goodsDescription" select="s0:DetailedDescription/text()"/>
            <xsl:with-param name="CTKNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='CTK']/s0:Value/text()"/>
          </xsl:call-template>
        </xsl:variable>
        <xsl:if test="$goodsDescriptionAndCTK!=''">
          <xsl:call-template name="FTX-AAA-Code">
            <xsl:with-param name="text" select="$goodsDescriptionAndCTK"/>
          </xsl:call-template>
        </xsl:if>

        <xsl:call-template name="GID-NAD">
          <xsl:with-param name="packingLine" select="."/>
        </xsl:call-template>

        <xsl:call-template name="GID-MEA">
          <xsl:with-param name="packingLine" select="."/>
          <xsl:with-param name="unitOfWeight" select="$unitOfWeight"/>
          <xsl:with-param name="unitOfVolume" select="$unitOfVolume"/>
        </xsl:call-template>

        <xsl:call-template name="GID-RFF">
          <xsl:with-param name="packingLine" select="."/>
          <xsl:with-param name="dischargePortPrefix" select="$dischargePortPrefix"/>
          <xsl:with-param name="loadPortPrefix" select="$loadPortPrefix"/>
        </xsl:call-template>

        <xsl:call-template name="GID-PCI">
          <xsl:with-param name="text" select="s0:MarksAndNos/text()"/>
        </xsl:call-template>

        <xsl:call-template name="GID-SGP">
          <xsl:with-param name="packingLines" select="."/>
          <xsl:with-param name="unitOfWeight" select="$unitOfWeight"/>
          <xsl:with-param name="unitOfVolume" select="$unitOfVolume"/>
        </xsl:call-template>

        <xsl:for-each select="s0:UNDGCollection/s0:UNDG[s0:UNDGCode/text()!='']">
          <xsl:if test="position() &lt;= 99">
            <xsl:call-template name="DGSLoop-Core">
              <xsl:with-param name="undgNode" select="."/>
              <xsl:with-param name="numberFormat" select="$numberFormat"/>
            </xsl:call-template>
          </xsl:if>
        </xsl:for-each>
      </ns0:GIDLoop1>
    </xsl:for-each>
  </xsl:template>

  <xsl:template name="GID">
    <xsl:param name="packingLine"/>

    <ns0:GID>
      <GID01>
        <xsl:value-of select="userCSharp:GIDCounter()"/>
      </GID01>
      <ns0:C213>
        <C21301>
          <xsl:value-of select="$packingLine/s0:PackQty/text()"/>
        </C21301>
        <C21302>
          <xsl:variable name="packageTypeCode" select="$packingLine/s0:PackType/s0:Code/text()"/>

          <xsl:variable name="packageTypeMappingV1" select="CodeMapper:GetRecipientCode($CarrierName , $CarrierName , $combineRecipientMappingNamev1, 'Package Type' , $combineRecipientCodeField, $packageTypeCode)"/>
          <xsl:choose>
            <xsl:when test="$packageTypeMappingV1!=''">
              <xsl:value-of select="$packageTypeMappingV1"/>
            </xsl:when>
            <xsl:otherwise>
              <xsl:value-of select="CodeMapper:GetRecipientCode('SHIPPING_INSTRUCTION' , 'SHIPPING_INSTRUCTION' , 'OCM System Configuration', 'Package Type ISO' , 'Package Type', $packageTypeCode)"/>
            </xsl:otherwise>
          </xsl:choose>
        </C21302>
        <C21304>6</C21304>
        <C21305>
          <xsl:value-of select="$packingLine/s0:PackType/s0:Description/text()" />
        </C21305>
      </ns0:C213>
    </ns0:GID>
  </xsl:template>

  <xsl:template name="GID-PIA">
    <xsl:param name="packingLine"/>
    <xsl:param name="isSummary" select="'FALSE'"/>
    <xsl:param name="key" select="''"/>

    <xsl:if test="$isSummary = 'FALSE'">
      <xsl:variable name="clearClassificationCodes" select="ListHelperClear:ClearList()" />
    </xsl:if>

    <xsl:call-template name="PIA-Core">
      <xsl:with-param name="code" select="$packingLine/s0:HarmonisedCode/text()"/>
      <xsl:with-param name="key" select="$key"/>
    </xsl:call-template>
    <xsl:for-each select="$packingLine/s0:ClassificationCollection/s0:Classification[s0:Type/s0:Code/text() = 'HSC']">
      <xsl:call-template name="PIA-Core">
        <xsl:with-param name="code" select="s0:Code/text()"/>
        <xsl:with-param name="key" select="$key"/>
      </xsl:call-template>
    </xsl:for-each>
    <xsl:for-each select="$packingLine/s0:ClassificationCollection/s0:Classification[s0:Type/s0:Code/text() = 'HSC' and (s0:Country/s0:Code = '' or not(s0:Country))]">
      <xsl:call-template name="PIA-Core">
        <xsl:with-param name="code" select="s0:Code/text()"/>
        <xsl:with-param name="key" select="$key"/>
      </xsl:call-template>
    </xsl:for-each>
    <xsl:for-each select="$packingLine/s0:ClassificationCollection/s0:Classification[s0:Type/s0:Code/text() = 'HSC' and s0:Country/s0:Code != '']">
      <xsl:call-template name="PIA-Core">
        <xsl:with-param name="code" select="s0:Code/text()"/>
        <xsl:with-param name="key" select="$key"/>
      </xsl:call-template>
    </xsl:for-each>
  </xsl:template>

  <xsl:template name="PIA-Core">
    <xsl:param name="code"/>
    <xsl:param name="key"/>

    <xsl:variable name="classificationCode" select="StringHelper:StringReplace($code, '.', '')"/>
    <xsl:if test="$classificationCode != ''">
      <xsl:variable name="needToAddClassificationCode" select="ListHelperClear:ShouldCreateItem(concat('PIA+', $key), $classificationCode, 2)"/>
      <xsl:if test="$needToAddClassificationCode">
        <ns0:PIA>
          <PIA01>5</PIA01>
          <ns0:C212>
            <C21201>
              <xsl:value-of select="$classificationCode"/>
            </C21201>
            <C21202>HS</C21202>
          </ns0:C212>
        </ns0:PIA>
      </xsl:if>
    </xsl:if>
  </xsl:template>

  <xsl:template name="GID-NAD">
    <xsl:param name="packingLine"/>

    <xsl:if test="$ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
      <xsl:variable name="subShipmentNumber" select="s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='SubShipmentNumber']/s0:Value/text()" />
      <xsl:variable name="organizationAddressCollection" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:OrganizationAddressCollection" />
      <xsl:if test="$subShipmentNumber != '' and $organizationAddressCollection">
        <xsl:variable name="consignorDocumentaryAddress" select="$organizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsignorDocumentaryAddress']"/>
        <xsl:variable name="consigneeDocumentaryAddress" select="$organizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='ConsigneeDocumentaryAddress']"/>
        <xsl:variable name="notifyParty" select="$organizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='NotifyParty']"/>
        <xsl:variable name="supplierDocumentaryAddress">
          <xsl:call-template name="NAD-Org-Select">
            <xsl:with-param name="org1" select="$organizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='SupplierDocumentaryAddress']"/>
            <xsl:with-param name="org2" select="$consignorDocumentaryAddress"/>
          </xsl:call-template>
        </xsl:variable>
        <xsl:variable name="buyerDocumentaryAddress">
          <xsl:call-template name="NAD-Org-Select">
            <xsl:with-param name="org1" select="$organizationAddressCollection/s0:OrganizationAddress[s0:AddressType/text()='BuyerDocumentaryAddress']"/>
            <xsl:with-param name="org2" select="$consigneeDocumentaryAddress"/>
          </xsl:call-template>
        </xsl:variable>

        <xsl:call-template name="NAD-Org">
          <xsl:with-param name="code" select="'OS'"/>
          <xsl:with-param name="org" select="$consignorDocumentaryAddress"/>
          <xsl:with-param name="suffix" select="'_2'"/>
        </xsl:call-template>
        <xsl:call-template name="NAD-Org">
          <xsl:with-param name="code" select="'UC'"/>
          <xsl:with-param name="org" select="$consigneeDocumentaryAddress"/>
          <xsl:with-param name="suffix" select="'_2'"/>
          <xsl:with-param name="portOfDestination" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:PortOfDestination/s0:Code/text()"/>
        </xsl:call-template>
        <xsl:call-template name="NAD-Org">
          <xsl:with-param name="code" select="'SE'"/>
          <xsl:with-param name="org" select="msxsl:node-set($supplierDocumentaryAddress)/s0:OrganizationAddress"/>
          <xsl:with-param name="suffix" select="'_2'"/>
        </xsl:call-template>
        <xsl:call-template name="NAD-Org">
          <xsl:with-param name="code" select="'BY'"/>
          <xsl:with-param name="org" select="msxsl:node-set($buyerDocumentaryAddress)/s0:OrganizationAddress"/>
          <xsl:with-param name="suffix" select="'_2'"/>
          <xsl:with-param name="portOfDestination" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:PortOfDestination/s0:Code/text()"/>
        </xsl:call-template>
        <xsl:call-template name="NAD-Org">
          <xsl:with-param name="code" select="'ZZZ'"/>
          <xsl:with-param name="org" select="$notifyParty"/>
          <xsl:with-param name="suffix" select="'_2'"/>
          <xsl:with-param name="portOfDestination" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:PortOfDestination/s0:Code/text()"/>
        </xsl:call-template>
      </xsl:if>
    </xsl:if>
  </xsl:template>

  <xsl:template name="GID-MEA">
    <xsl:param name="packingLine"/>
    <xsl:param name="unitOfWeight"/>
    <xsl:param name="unitOfVolume"/>

    <ns0:MEALoop1>
      <xsl:call-template name="MEA">
        <xsl:with-param name="code" select="'AAE'"/>
        <xsl:with-param name="type" select="'WT'"/>
        <xsl:with-param name="unit" select="$unitOfWeight"/>
        <xsl:with-param name="value" select="StringMapper:FormatDecimal($packingLine/s0:Weight/text(),'0.###',false())"/>
        <xsl:with-param name="suffix" select="''"/>
      </xsl:call-template>
    </ns0:MEALoop1>

    <ns0:MEALoop1>
      <xsl:call-template name="MEA">
        <xsl:with-param name="code" select="'AAE'"/>
        <xsl:with-param name="type" select="'AAW'"/>
        <xsl:with-param name="unit" select="$unitOfVolume"/>
        <xsl:with-param name="value" select="StringMapper:FormatDecimal($packingLine/s0:Volume/text(),'0.####',false())"/>
        <xsl:with-param name="suffix" select="''"/>
      </xsl:call-template>
    </ns0:MEALoop1>
  </xsl:template>

  <xsl:template name="GID-RFF">
    <xsl:param name="packingLine"/>
    <xsl:param name="dischargePortPrefix"/>
    <xsl:param name="loadPortPrefix"/>

    <xsl:variable name="clearSG22RFF" select="ListHelperClear:ClearList()" />

    <xsl:if test="$dischargePortPrefix='BR' or $loadPortPrefix='BR'">
      <xsl:variable name="harmonisedCode" select="StringHelper:StringReplace($packingLine/s0:HarmonisedCode/text(), '.', '')"/>
      <xsl:if test=" $harmonisedCode!='' and ListHelperClear:ShouldCreateItem('SG22RFF+', concat('ABT+', $harmonisedCode), 9)">
        <xsl:call-template name="RFF">
          <xsl:with-param name="suffix" select="'4'"/>
          <xsl:with-param name="code" select="'ABT'"/>
          <xsl:with-param name="ref" select="$harmonisedCode"/>
        </xsl:call-template>
      </xsl:if>
      <xsl:for-each select="$packingLine/s0:ClassificationCollection/s0:Classification[s0:Country/s0:Code/text()='BR']">
        <xsl:variable name="classificationCode" select="StringHelper:StringReplace(s0:Code/text(), '.', '')"/>
        <xsl:if test="$classificationCode!='' and ListHelperClear:ShouldCreateItem('SG22RFF+', concat('ABT+', $classificationCode), 9)">
          <xsl:call-template name="RFF">
            <xsl:with-param name="suffix" select="'4'"/>
            <xsl:with-param name="code" select="'ABT'"/>
            <xsl:with-param name="ref" select="$classificationCode"/>
          </xsl:call-template>
        </xsl:if>
      </xsl:for-each>
    </xsl:if>

    <xsl:if test="$ICS2FillingType_CarrierWithMoreThanOneSubShipment='TRUE'">
      <xsl:variable name="subShipmentNumber" select="$packingLine/s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='SubShipmentNumber']/s0:Value/text()" />
      <xsl:if test="$subShipmentNumber!=''">
        <xsl:variable name="subShipmentWaybillNumber" select="$subShipmentCollection/s0:SubShipment[s0:DataContext/s0:DataSourceCollection/s0:DataSource/s0:Key/text()=$subShipmentNumber]/s0:WayBillNumber/text()" />
        <xsl:if test="$subShipmentWaybillNumber!='' and ListHelperClear:ShouldCreateItem('SG22RFF+', concat('BH+', $subShipmentWaybillNumber), 9)">
          <xsl:call-template name="RFF">
            <xsl:with-param name="suffix" select="'4'"/>
            <xsl:with-param name="code" select="'BH'"/>
            <xsl:with-param name="ref" select="$subShipmentWaybillNumber"/>
          </xsl:call-template>
        </xsl:if>
      </xsl:if>
    </xsl:if>

    <xsl:call-template name="RFF-TN">
      <xsl:with-param name="ITNNumber" select="$packingLine/s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='ITN']/s0:Value/text()" />
      <xsl:with-param name="DUENumber" select="$packingLine/s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='DUE']/s0:Value/text()" />
      <xsl:with-param name="exportReferenceNumber" select="$packingLine/s0:ExportReferenceNumber/text()"/>
      <xsl:with-param name="CTNNumber" select="$packingLine/s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='CTN']/s0:Value/text()" />
      <xsl:with-param name="MRNNumber" select="$packingLine/s0:AddInfoCollection/s0:AddInfo[s0:Key/text()='MRN']/s0:Value/text()" />
    </xsl:call-template>

    <xsl:for-each select="$packingLine/s0:ClassificationCollection/s0:Classification[s0:Type/s0:Code/text() = 'CUS']">
      <xsl:variable name="classificationCode" select="StringHelper:StringReplace(s0:Code/text(), '.', '')"/>
      <xsl:if test="$classificationCode!='' and ListHelperClear:ShouldCreateItem('SG22RFF+', concat('AIY+', $classificationCode), 9)">
        <xsl:call-template name="RFF">
          <xsl:with-param name="suffix" select="'4'"/>
          <xsl:with-param name="code" select="'AIY'"/>
          <xsl:with-param name="ref" select="$classificationCode"/>
        </xsl:call-template>
      </xsl:if>
    </xsl:for-each>
  </xsl:template>

  <xsl:template name="GID-PCI">
    <xsl:param name="text"/>

    <xsl:call-template name="PCI-Core">
      <xsl:with-param name="text" select="StringHelper:TrimCRLF($text)"/>
      <xsl:with-param name="segmentCount" select="1"/>
    </xsl:call-template>
  </xsl:template>

  <xsl:template name="PCI-Core">
    <xsl:param name="text"/>
    <xsl:param name="segmentCount"/>
    <xsl:if test="$text!='' and number($segmentCount)&lt;=50">
      <xsl:variable name="lineLength" select="userCSharp:GetLengthForWrapping($text, 35)"/>
      <xsl:variable name="pciText" select="substring($text, 1, $lineLength)"/>
      <xsl:variable name="pciTextNorm" select="normalize-space($pciText)"/>
      <xsl:variable name="remainingText" select="StringHelper:TrimCRLF(substring($text, $lineLength+1))"/>
      <xsl:if test="$pciTextNorm!=''">
        <ns0:PCILoop1>
          <ns0:PCI>
            <ns0:C210>
              <C21001>
                <xsl:value-of select="$pciTextNorm"/>
              </C21001>
            </ns0:C210>
          </ns0:PCI>
        </ns0:PCILoop1>
      </xsl:if>
      <xsl:if test="$remainingText!=''">
        <xsl:call-template name="PCI-Core">
          <xsl:with-param name="text" select="$remainingText"/>
          <xsl:with-param name="segmentCount" select="$segmentCount+1"/>
        </xsl:call-template>
      </xsl:if>
    </xsl:if>
  </xsl:template>

  <xsl:template name="GID-SGP">
    <xsl:param name="packingLines"/>
    <xsl:param name="unitOfWeight"/>
    <xsl:param name="unitOfVolume"/>

    <xsl:for-each select="$packingLines[s0:ContainerNumber/text() != '']">
      <ns0:SGPLoop1>
        <ns0:SGP>
          <ns0:C237>
            <C23701>
              <xsl:value-of select="s0:ContainerNumber/text()"/>
            </C23701>
          </ns0:C237>
          <SGP02>
            <xsl:value-of select="s0:PackQty/text()"/>
          </SGP02>
        </ns0:SGP>
        <ns0:MEALoop3>
          <xsl:call-template name="MEA">
            <xsl:with-param name="code" select="'AAE'"/>
            <xsl:with-param name="type" select="'WT'"/>
            <xsl:with-param name="unit" select="$unitOfWeight"/>
            <xsl:with-param name="value" select="StringMapper:FormatDecimal(s0:Weight/text(),'0.###',false())"/>
            <xsl:with-param name="suffix" select="'_3'"/>
          </xsl:call-template>
        </ns0:MEALoop3>
        <ns0:MEALoop3>
          <xsl:call-template name="MEA">
            <xsl:with-param name="code" select="'AAE'"/>
            <xsl:with-param name="type" select="'AAW'"/>
            <xsl:with-param name="unit" select="$unitOfVolume"/>
            <xsl:with-param name="value" select="StringMapper:FormatDecimal(s0:Volume/text(),'0.####',false())"/>
            <xsl:with-param name="suffix" select="'_3'"/>
          </xsl:call-template>
        </ns0:MEALoop3>
      </ns0:SGPLoop1>
    </xsl:for-each>
  </xsl:template>

  <xsl:template name="DGSLoop-Core">
    <xsl:param name="undgNode"/>
    <xsl:param name="numberFormat"/>

    <ns0:DGSLoop1>
      <ns0:DGS>
        <DGS01>IMD</DGS01>
        <ns0:C205>
          <C20501>
            <xsl:value-of select="$undgNode/s0:IMOClass/text()"/>
          </C20501>
          <C20502>
            <xsl:value-of select="$undgNode/s0:SubLabel1/text()"/>
          </C20502>
        </ns0:C205>
        <ns0:C234>
          <C23401>
            <xsl:value-of select="substring($undgNode/s0:UNDGCode/text(),1,4)"/>
          </C23401>
        </ns0:C234>
        <xsl:if test="$undgNode/s0:FlashPoint/text()!='0.0'">
          <ns0:C223>
            <C22301>
              <xsl:choose>
                <xsl:when test="contains($numberFormat, 'DGS=')">
                  <xsl:variable name="numberFormatDGS" select="userCSharp:GetNumberFormat($numberFormat,'DGS=',1)" />
                  <xsl:value-of select="userCSharp:FormatTemp(StringMapper:FormatDecimal($undgNode/s0:FlashPoint/text(),$numberFormatDGS,false()))"/>
                </xsl:when>
                <xsl:otherwise>
                  <xsl:value-of select="userCSharp:FormatTemp($undgNode/s0:FlashPoint/text())"/>
                </xsl:otherwise>
              </xsl:choose>
            </C22301>
            <C22302>CEL</C22302>
          </ns0:C223>
        </xsl:if>
        <DGS05>
          <xsl:variable name="packinggroup" select="$undgNode/s0:PackingGroup/text()"/>
          <xsl:choose>
            <xsl:when test="$packinggroup='I'">1</xsl:when>
            <xsl:when test="$packinggroup='II'">2</xsl:when>
            <xsl:when test="$packinggroup='III'">3</xsl:when>
          </xsl:choose>
        </DGS05>
      </ns0:DGS>
      <xsl:call-template name="FTX-AAD">
        <xsl:with-param name="text" select="$undgNode/s0:ProperShippingName/text()"/>
      </xsl:call-template>
      <xsl:call-template name="DGS-FTX">
        <xsl:with-param name="code" select="'AAC'"/>
        <xsl:with-param name="text" select="$undgNode/s0:TechicalName/text()"/>
      </xsl:call-template>
      <xsl:if test="$undgNode/s0:MarinePollutant/s0:Description/text()!=''">
        <xsl:call-template name="DGS-FTX">
          <xsl:with-param name="code" select="'AAC'"/>
          <xsl:with-param name="text" select="'MARINE POLLUTANT'"/>
        </xsl:call-template>
      </xsl:if>

      <xsl:if test="$undgNode/s0:Contact/s0:FullName/text()!='' and $undgNode/s0:Contact/s0:Phone/text()!=''">
        <ns0:CTALoop2>
          <ns0:CTA_3>
            <CTA01>HG</CTA01>
            <ns0:C056_3>
              <C05602>
                <xsl:value-of select="normalize-space(substring($undgNode/s0:Contact/s0:FullName/text(),1,35))"/>
              </C05602>
            </ns0:C056_3>
          </ns0:CTA_3>
          <xsl:if test="$undgNode/s0:Contact/s0:Phone/text()!=''">
            <ns0:COM_3>
              <ns0:C076_3>
                <C07601>
                  <xsl:value-of select="$undgNode/s0:Contact/s0:Phone/text()"/>
                </C07601>
                <C07602>TE</C07602>
              </ns0:C076_3>
            </ns0:COM_3>
          </xsl:if>
        </ns0:CTALoop2>
      </xsl:if>
    </ns0:DGSLoop1>
  </xsl:template>

  <xsl:template name="FTX-AAA-Code">
    <xsl:param name="text"/>
    <xsl:variable name="allowEmptySegment" select="CodeMapper:GetRecipientCode($MappingId, $MappingId, $MappingDescription, 'FTX Format', 'AllowEmptySegment', $RecipientID, $OrgSCAC, 'AAA')"/>

    <xsl:if test="normalize-space($text)!=''">
      <xsl:call-template name="FTX-AAA-Code-Core">
        <xsl:with-param name="text" select="userCSharp:ReplaceCRLFText($text)"/>
        <xsl:with-param name="allowEmptySegment" select="$allowEmptySegment"/>
      </xsl:call-template>
    </xsl:if>
  </xsl:template>

  <xsl:template name="FTX-AAA-Code-Core">
    <xsl:param name="text"/>
    <xsl:param name="allowEmptySegment"/>
    <xsl:if test="userCSharp:ShouldCreateFTX() and $text != ''">
      <xsl:variable name="lineLength" select="userCSharp:GetLengthForWrapping($text, 26)"/>
      <xsl:variable name="ftxText" select="substring($text, 1, $lineLength)"/>
      <xsl:variable name="ftxTextNorm" select="normalize-space($ftxText)"/>
      <xsl:variable name="remainingText" select="substring($text, $lineLength+1)"/>

      <xsl:if test="StringHelper:ToUpper($allowEmptySegment)='TRUE' or userCSharp:RemoveAllSpaceFromLineWithoutTextButSpace($ftxTextNorm)!=''">
        <ns0:FTX_5>
          <FTX01>
            <xsl:text>AAA</xsl:text>
          </FTX01>
          <ns0:C108_5>
            <C10801>
              <xsl:value-of select="$ftxTextNorm"/>
            </C10801>
          </ns0:C108_5>
        </ns0:FTX_5>
        <xsl:variable name="setFTXCounter" select="userCSharp:SetFTXCounter()" />
      </xsl:if>
      <xsl:call-template name="FTX-AAA-Code-Core">
        <xsl:with-param name="text" select="$remainingText"/>
        <xsl:with-param name="allowEmptySegment" select="$allowEmptySegment"/>
      </xsl:call-template>
    </xsl:if>
  </xsl:template>

  <xsl:template name="SEL">
    <xsl:param name="sealNumber"/>
    <xsl:param name="sealParty"/>
    <xsl:if test="$sealNumber!=''">
      <ns0:SEL_3>
        <SEL01>
          <xsl:value-of select="normalize-space(substring($sealNumber,1,15))"/>
        </SEL01>
        <ns0:C215_3>
          <C21501>
            <xsl:choose>
              <xsl:when test="$sealParty='QRT'">AC</xsl:when>
              <xsl:when test="$sealParty='CAR'">CA</xsl:when>
              <xsl:when test="$sealParty='CRD'">SH</xsl:when>
              <xsl:when test="$sealParty='CTO'">TO</xsl:when>
              <xsl:when test="$sealParty='CUS'">CU</xsl:when>
            </xsl:choose>
          </C21501>
        </ns0:C215_3>
      </ns0:SEL_3>
    </xsl:if>
  </xsl:template>

  <xsl:template name="DGS-FTX">
    <xsl:param name="code"/>
    <xsl:param name="text"/>

    <xsl:if test="$text!=''">
      <ns0:FTX_7>
        <FTX01>
          <xsl:value-of select="$code"/>
        </FTX01>
        <ns0:C108_7>
          <C10801>
            <xsl:value-of select="$text"/>
          </C10801>
        </ns0:C108_7>
      </ns0:FTX_7>
    </xsl:if>

  </xsl:template>

  <xsl:template name="FTX-AAD">
    <xsl:param name="text"/>
    <xsl:param name="count" select="1"/>

    <xsl:if test="$text!='' and $count&lt;=3">
      <xsl:variable name="length" select="userCSharp:GetLengthForWrapping($text, 30)"/>
      <xsl:variable name="ftxText" select="normalize-space(substring($text, 1, $length))"/>
      <xsl:if test="translate($ftxText, ' ', '')!=''">
        <ns0:FTX_7>
          <FTX01>AAD</FTX01>
          <ns0:C108_7>
            <C10801>
              <xsl:value-of select="$ftxText"/>
            </C10801>
          </ns0:C108_7>
        </ns0:FTX_7>
      </xsl:if>
      <xsl:call-template name="FTX-AAD">
        <xsl:with-param name="text" select="substring($text, $length+1)"/>
        <xsl:with-param name="count" select="$count+1"/>
      </xsl:call-template>
    </xsl:if>
  </xsl:template>

  <xsl:template name="AddRFF">
    <xsl:param name="org"/>
    <xsl:param name="addressType"/>
    <xsl:param name="countryOfIssue"/>
    <xsl:param name="portOfDestination"/>

    <xsl:choose>
      <xsl:when test="$countryOfIssue='IN'">
        <xsl:for-each select="$org/s0:RegistrationNumberCollection/s0:RegistrationNumber[s0:CountryOfIssue/s0:Code/text()='IN' and s0:Type/s0:Code/text()='PAN' and s0:Value/text()!='']">
          <xsl:variable name="addRFF" select="MultipleRFFListHelper:AddRFF(s0:Value/text(), s0:RegulatingCountry/text(), $countryOfIssue, $addressType, s0:Type/s0:Code/text())" />
        </xsl:for-each>
        <xsl:for-each select="$org/s0:RegistrationNumberCollection/s0:RegistrationNumber[s0:CountryOfIssue/s0:Code/text()='IN' and s0:Type/s0:Code/text()='IEC' and s0:Value/text()!='']">
          <xsl:variable name="addRFF" select="MultipleRFFListHelper:AddRFF(s0:Value/text(), s0:RegulatingCountry/text(), $countryOfIssue, $addressType, s0:Type/s0:Code/text())" />
        </xsl:for-each>
        <xsl:for-each select="$org/s0:RegistrationNumberCollection/s0:RegistrationNumber[s0:CountryOfIssue/s0:Code/text()='IN' and s0:Type/s0:Code/text()!='PAN' and s0:Type/s0:Code/text()!='IEC' and s0:Value/text()!='']">
          <xsl:variable name="addRFF" select="MultipleRFFListHelper:AddRFF(s0:Value/text(), s0:RegulatingCountry/text(), $countryOfIssue, $addressType, s0:Type/s0:Code/text())" />
        </xsl:for-each>
      </xsl:when>
      <xsl:otherwise>
        <xsl:for-each select="$org/s0:RegistrationNumberCollection/s0:RegistrationNumber[not(s0:CountryOfIssue/s0:Code/text()='IN' and s0:Type/s0:Code/text()='IEC')]">
          <xsl:if test="s0:Type/s0:Code/text() != 'EOR' or (s0:Type/s0:Code/text() = 'EOR' and ICS2Helper:IsICS2Port($portOfDestination))">
            <xsl:variable name="addRFF" select="MultipleRFFListHelper:AddRFF(s0:Value/text(), s0:RegulatingCountry/text(), $countryOfIssue, $addressType, s0:Type/s0:Code/text(), false())" />
          </xsl:if>
        </xsl:for-each>
      </xsl:otherwise>
    </xsl:choose>

  </xsl:template>

  <xsl:template name="GetValueOrDefault">
    <xsl:param name="value1" select="''"/>
    <xsl:param name="value2" select="''"/>
    <xsl:param name="value3" select="''"/>
    <xsl:param name="value4" select="''"/>
    <xsl:param name="value5" select="''"/>
    <xsl:param name="defaultValue" select="''"/>

    <xsl:choose>
      <xsl:when test="$value1 != ''">
        <xsl:value-of select="$value1"/>
      </xsl:when>
      <xsl:when test="$value2 != ''">
        <xsl:value-of select="$value2"/>
      </xsl:when>
      <xsl:when test="$value3 != ''">
        <xsl:value-of select="$value3"/>
      </xsl:when>
      <xsl:when test="$value4 != ''">
        <xsl:value-of select="$value4"/>
      </xsl:when>
      <xsl:when test="$value5 != ''">
        <xsl:value-of select="$value5"/>
      </xsl:when>
      <xsl:otherwise>
        <xsl:value-of select="$defaultValue"/>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="GetGoodsDescriptionAndCTK">
    <xsl:param name="goodsDescription"/>
    <xsl:param name="CTKNumber"/>

    <xsl:choose>
      <xsl:when test="$CTKNumber != ''">
        <xsl:value-of select="concat($goodsDescription,' Cargo Tracking Note: ',$CTKNumber)"/>
      </xsl:when>
      <xsl:otherwise>
        <xsl:value-of select="$goodsDescription"/>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <msxsl:script language="C#" implements-prefix="userCSharp">
    <![CDATA[

public System.Collections.Generic.Dictionary<string,decimal> dictPacklineVolume = new System.Collections.Generic.Dictionary<string,decimal>();
public System.Collections.Generic.Dictionary<string,decimal> dictPacklineWeight = new System.Collections.Generic.Dictionary<string,decimal>();

public void CalculatePacklineVolume(string containerNumber, decimal volume)
{
  decimal packlineVolume = volume != null ? volume : 0;

  if (!dictPacklineVolume.ContainsKey(containerNumber))
  {
    dictPacklineVolume.Add(containerNumber, packlineVolume);
  }
  else
  {
    dictPacklineVolume[containerNumber] += packlineVolume;
  }
}

public string GetTotalPacklineVolumeByContainerNumber(string containerNumber)
{
  decimal result = 0;

  if (dictPacklineVolume.ContainsKey(containerNumber))
  {
    result = dictPacklineVolume[containerNumber];
  }

  return result.ToString();
}

public void CalculatePacklineWeight(string containerNumber, decimal weight)
{
  decimal packlineWeight = weight != null ? weight : 0;

  if (!dictPacklineWeight.ContainsKey(containerNumber))
  {
    dictPacklineWeight.Add(containerNumber, packlineWeight);
  }
  else
  {
    dictPacklineWeight[containerNumber] += packlineWeight;
  }
}

public string GetTotalPacklineWeightByContainerNumber(string containerNumber)
{
  decimal result = 0;

  if (dictPacklineWeight.ContainsKey(containerNumber))
  {
    result = dictPacklineWeight[containerNumber];
  }

  return result.ToString();
}

int gidCounter = 1;
public int GIDCounter()
{
  return gidCounter++;
}

public string MultiplyDouble(string valueText1, string valueText2, string format)
{
  Double value1;
  Double value2;
  if (!Double.TryParse(valueText1, out value1)) return string.Empty;
  if (!Double.TryParse(valueText2, out value2)) return string.Empty;
  return (value1 * value2).ToString(format);
}


public string FormatTemp(string text)
{
  Double temp;
  if (!Double.TryParse(text, out temp))
    return text;
  string tempfmt = Math.Abs(temp).ToString("G3");
  return (temp < 0 ? "-" : "") + new String('0', (tempfmt.Contains(".") ? 4 : 3) - tempfmt.Length) + tempfmt;
}

public string GetCustomAddress(string code, string name, string address1, string address2)
{
  if (code != "CN" && code != "NI" && code != "N1" && code != "N2")
    return String.Empty;

  name = name.ToUpperInvariant();
  address1 = address1.ToUpperInvariant();
  address2 = address2.ToUpperInvariant();

  if (name == "AS ABOVE" || address1 == "AS ABOVE" || address2 == "AS ABOVE")
    return "AS ABOVE";
  if (IsSameAs(name))
    return name;
  if (IsSameAs(address1))
    return address1;
  if (IsSameAs(address2))
    return address2;

  return String.Empty;
}

bool IsSameAs(string value)
{
  return value.StartsWith("SAME AS");
}

public int GetLengthForWrapping(string text, string maxLengthStr)
{
  int maxLength = int.Parse(maxLengthStr);

  if (text.Substring(0,1) == "\n")
  {
    return 1;
  }

  for (int i = 1; i < text.Length && i < maxLength + 1; i++)
  {
    if (text[i] == '\n')
    {
      return i > maxLength ? i : i + 1;
    }
  }

  if (text.Length <= maxLength)
  {
    return text.Length;
  }

  for (int i = maxLength; i >= 0; i--)
  {
    if (text[i] == ' ')
    {
      return i == 0 ? 1 : i;
    }
  }

  return maxLength;
}

public string ReplaceCRLFText(string text)
{
  if (text != null && text.Length > 0)
  {
    return text.Replace("\r\n", "\n");
  }
  return "";
}

public string ToUpper(string input)
{
  string result = "";
  if (input != null)
  {
    result = input;
  }
  return result.ToUpper();
}

public string ThrowPartyReceiverIDNotFound(string carrierID, string carrierSCAC)
{
  throw new ArgumentException(string.Format(@"Could not found matching PartyReceiverID in the UNB3 Lookup code mapping.(Sender: * - Multiple senders, Recipient: SHIPPING_INSTRUCTION, Interface: OCM System Configuration, Code Set: UNB3, Input: [CarrierID:{0}], [SCAC:{1}])", carrierID, carrierSCAC));
}

public int GetEndIndex(string fullAddress, int startIndex)
{
  if (fullAddress.Length < startIndex) return 0;

  if (fullAddress.Length <= startIndex + 34)
  {
    return SubstringSafe(fullAddress, startIndex, fullAddress.Length - startIndex).Length;
  }

  var subStringAddress = SubstringSafe(fullAddress, startIndex, 35);

  if (!subStringAddress.Contains(" "))
  {
    return subStringAddress.Length - 1;
  }

  if (subStringAddress.Length == 35)
  {
    if (startIndex + 35 == fullAddress.Length)
    {
      return subStringAddress.Length - 1;
    }
    else if (SubstringSafe(fullAddress, startIndex + 35, 1) == " ")
    {
      return subStringAddress.Length;
    }
  }

  for (int i = startIndex + 34; i > -1; i--)
  {
    string subString = SubstringSafe(subStringAddress, i, 1);

    if (subString == " ")
    {
      return i;
    }
  }
  return startIndex;;
}

public string SubstringSafe(string text, int startIndex, int length)
{
  string result = "";

  if (startIndex < 0)
  {
    startIndex = 0;
  }

  int actualLength = text.Length - startIndex;
  if (actualLength > 0)
  {
    if (actualLength > length && length >= 0)
    {
      actualLength = length;
    }
    result = text.Substring(startIndex, actualLength);
  }

  return result;
}

public string ShrinkSpaces(string str)
{
  string[] arry = str.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
  return string.Join(" ", arry);
}

int ftxCounter = 1;
public bool ShouldCreateFTX()
{
  //FTX Max Segment = 99
  return ftxCounter <= 99 ? true : false;
}

public void SetFTXCounter()
{
  ftxCounter++;
}

public void ResetFTXCounter()
{
  ftxCounter = 1;
}

int ftxSegmentCounter = 1;

public bool ShouldCreateFTXSegment(int noOfSegment)
{
  return ftxSegmentCounter <= noOfSegment ? true : false;
}

public int GetFTXSegmentCounter()
{
  return ftxSegmentCounter;
}

public string GetFTXFormat(int noOfSegment, int linelen, string text)
{
  var result = SubstringSafe(text, linelen*(noOfSegment-1), linelen);
  ftxSegmentCounter++;
  return result;
}

public void ResetFTXSegmentCounter()
{
  ftxSegmentCounter = 1;
}

public string GetNumberFormat(string text, string type, int defaultFormat)
{
  var result = "0." + new string('#', defaultFormat);
  var sections = text.Split(';');
  foreach (string s in sections)
  {
    if (s.Contains(type))
    {
      int number = int.Parse(s.Replace(type,""));
      if (number>0)
      {
        result = "0." + new string('#', number);
      }
      else
      {
        return "#";
      }
    }
  }
  return result;
}

public string RemoveAllSpaceFromLineWithoutTextButSpace(string text)
{
  string result = "";
  if (text != null && text.Length > 0)
  {
   result = text.Replace(" ", "").Trim();
  }

  return result == ""? result : text;
}

public bool IsContainedSCAC(string scac)
{
    System.Collections.Generic.List<string> SCASList = new System.Collections.Generic.List<string> { "CMDU", "ANNU", "ANLC", "APLU", "CHNL", "CNAH", "USLU" };
    return !SCASList.Contains(scac);
}

public bool IsToOrder(string value)
{
  return value.StartsWith("TO ORDER") ||
         value.StartsWith("TO THE ORDER");
}

public int? firstLegOrder;
public int? lastLegOrder;

public string FindFirstAndLastLegOrder(string legOrder)
{
  var number = int.Parse(legOrder);

  if (!firstLegOrder.HasValue || number < firstLegOrder.Value)
  {
    firstLegOrder = number;
  }

  if (!lastLegOrder.HasValue || number > lastLegOrder.Value)
  {
    lastLegOrder = number;
  }

  return string.Empty;
}

public int GetFirstLegOrder()
{
  return firstLegOrder.Value;
}

public int GetLastLegOrder()
{
  return lastLegOrder.Value;
}
]]>
  </msxsl:script>
</xsl:stylesheet>
