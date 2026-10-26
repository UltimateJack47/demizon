import 'package:json_annotation/json_annotation.dart';

part 'costume.g.dart';

/// Protějšky `Demizon.Contracts/Costumes/*`. Enumy chodí jako text
/// (`Individual`, `Pending`…), časy jako UTC se `Z`.

/// Kus nebo šarže. `valueCzk` dostane jen ten, kdo vidí celou krojárku.
@JsonSerializable()
class CostumeItem {
  const CostumeItem({
    required this.id,
    required this.code,
    required this.partId,
    required this.partName,
    this.costumeName,
    required this.gender,
    required this.trackingMode,
    this.size,
    this.color,
    this.note,
    this.valueCzk,
    required this.quantity,
    required this.state,
  });

  final int id;
  final String code;
  final int partId;
  final String partName;
  final String? costumeName;
  final String gender;
  final String trackingMode;
  final String? size;
  final String? color;
  final String? note;
  final int? valueCzk;
  final int quantity;
  final String state;

  bool get isBulk => trackingMode == 'Bulk';

  /// „Kopanický · Vesta · vel. 4 · černá“ — stejně jako web (`CostumeUi.Describe`).
  String get description => [
        costumeName,
        partName,
        if (size != null && size!.isNotEmpty) 'vel. $size',
        color,
      ].where((p) => p != null && p.isNotEmpty).join(' · ');

  factory CostumeItem.fromJson(Map<String, dynamic> json) => _$CostumeItemFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeItemToJson(this);
}

/// Člen v krojárce. `isDeleted`: bývalý člen, který pořád něco drží nebo držel.
@JsonSerializable()
class CostumeParty {
  const CostumeParty({required this.memberId, required this.fullName, required this.isDeleted});

  final int memberId;
  final String fullName;
  final bool isDeleted;

  factory CostumeParty.fromJson(Map<String, dynamic> json) => _$CostumePartyFromJson(json);
  Map<String, dynamic> toJson() => _$CostumePartyToJson(this);
}

@JsonSerializable(explicitToJson: true)
class CostumeHolding {
  const CostumeHolding({required this.item, required this.quantity});

  final CostumeItem item;
  final int quantity;

  factory CostumeHolding.fromJson(Map<String, dynamic> json) => _$CostumeHoldingFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeHoldingToJson(this);
}

@JsonSerializable(explicitToJson: true)
class CostumeHolder {
  const CostumeHolder({required this.member, required this.quantity});

  final CostumeParty member;
  final int quantity;

  factory CostumeHolder.fromJson(Map<String, dynamic> json) => _$CostumeHolderFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeHolderToJson(this);
}

/// Karta kusu po naskenování QR.
@JsonSerializable(explicitToJson: true)
class CostumeItemCard {
  const CostumeItemCard({required this.item, required this.inStore, required this.holders});

  final CostumeItem item;
  final int inStore;
  final List<CostumeHolder> holders;

  factory CostumeItemCard.fromJson(Map<String, dynamic> json) => _$CostumeItemCardFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeItemCardToJson(this);
}

/// Fotka bez dat; obrázek přes `ApiClient.getCostumePhoto`.
/// `isStale`: podle EXIF pořízená výrazně dřív, než byla nahrána.
@JsonSerializable()
class CostumePhoto {
  const CostumePhoto({required this.id, required this.uploadedAt, this.capturedAt, required this.isStale});

  final int id;
  final DateTime uploadedAt;
  final DateTime? capturedAt;
  final bool isStale;

  factory CostumePhoto.fromJson(Map<String, dynamic> json) => _$CostumePhotoFromJson(json);
  Map<String, dynamic> toJson() => _$CostumePhotoToJson(this);
}

@JsonSerializable(explicitToJson: true)
class CostumeTransferLine {
  const CostumeTransferLine({
    required this.id,
    required this.item,
    required this.quantity,
    this.conditionNote,
    required this.photos,
  });

  final int id;
  final CostumeItem item;
  final int quantity;
  final String? conditionNote;
  final List<CostumePhoto> photos;

  factory CostumeTransferLine.fromJson(Map<String, dynamic> json) => _$CostumeTransferLineFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeTransferLineToJson(this);
}

/// Předávka. `from` / `to` null = krojárka.
@JsonSerializable(explicitToJson: true)
class CostumeTransfer {
  const CostumeTransfer({
    required this.id,
    required this.status,
    this.from,
    this.to,
    required this.createdBy,
    required this.createdAt,
    this.submittedAt,
    this.resolvedAt,
    this.resolvedBy,
    this.note,
    this.rejectionReason,
    required this.lines,
  });

  final int id;
  final String status;
  final CostumeParty? from;
  final CostumeParty? to;
  final CostumeParty createdBy;
  final DateTime createdAt;
  final DateTime? submittedAt;
  final DateTime? resolvedAt;
  final CostumeParty? resolvedBy;
  final String? note;
  final String? rejectionReason;
  final List<CostumeTransferLine> lines;

  bool get isDraft => status == 'Draft';
  bool get isPending => status == 'Pending';
  bool get isOpen => isDraft || isPending;

  String get fromName => from?.fullName ?? 'Krojárka';
  String get toName => to?.fullName ?? 'Krojárka';

  factory CostumeTransfer.fromJson(Map<String, dynamic> json) => _$CostumeTransferFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeTransferToJson(this);
}

@JsonSerializable(explicitToJson: true)
class MyCostumes {
  const MyCostumes({required this.holdings, required this.incoming, required this.outgoing});

  final List<CostumeHolding> holdings;
  final List<CostumeTransfer> incoming;
  final List<CostumeTransfer> outgoing;

  factory MyCostumes.fromJson(Map<String, dynamic> json) => _$MyCostumesFromJson(json);
  Map<String, dynamic> toJson() => _$MyCostumesToJson(this);
}

@JsonSerializable()
class CostumeTransferStatusResponse {
  const CostumeTransferStatusResponse({required this.id, required this.status});

  final int id;
  final String status;

  factory CostumeTransferStatusResponse.fromJson(Map<String, dynamic> json) =>
      _$CostumeTransferStatusResponseFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeTransferStatusResponseToJson(this);
}

@JsonSerializable()
class CostumeMember {
  const CostumeMember({required this.id, required this.fullName});

  final int id;
  final String fullName;

  factory CostumeMember.fromJson(Map<String, dynamic> json) => _$CostumeMemberFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeMemberToJson(this);
}

@JsonSerializable(explicitToJson: true)
class CostumeHolderOverview {
  const CostumeHolderOverview({required this.member, required this.holdings});

  final CostumeParty member;
  final List<CostumeHolding> holdings;

  factory CostumeHolderOverview.fromJson(Map<String, dynamic> json) => _$CostumeHolderOverviewFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeHolderOverviewToJson(this);
}

// ------------------------------------------------------------- requesty

@JsonSerializable()
class CostumeLineRequest {
  const CostumeLineRequest({required this.code, this.quantity = 1});

  final String code;
  final int quantity;

  factory CostumeLineRequest.fromJson(Map<String, dynamic> json) => _$CostumeLineRequestFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeLineRequestToJson(this);
}

/// `fromMemberId` null = z krojárky. Příjemce `toMemberId` nebo `toQrToken`; bez obojího = do krojárky.
@JsonSerializable(explicitToJson: true)
class CreateCostumeTransferRequest {
  const CreateCostumeTransferRequest({
    this.fromMemberId,
    this.toMemberId,
    this.toQrToken,
    required this.lines,
    this.note,
  });

  final int? fromMemberId;
  final int? toMemberId;
  final String? toQrToken;
  final List<CostumeLineRequest> lines;
  final String? note;

  factory CreateCostumeTransferRequest.fromJson(Map<String, dynamic> json) =>
      _$CreateCostumeTransferRequestFromJson(json);
  Map<String, dynamic> toJson() => _$CreateCostumeTransferRequestToJson(this);
}

@JsonSerializable()
class CostumeLineNoteRequest {
  const CostumeLineNoteRequest({this.conditionNote});

  final String? conditionNote;

  factory CostumeLineNoteRequest.fromJson(Map<String, dynamic> json) => _$CostumeLineNoteRequestFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeLineNoteRequestToJson(this);
}

@JsonSerializable()
class RejectCostumeTransferRequest {
  const RejectCostumeTransferRequest({required this.reason});

  final String reason;

  factory RejectCostumeTransferRequest.fromJson(Map<String, dynamic> json) =>
      _$RejectCostumeTransferRequestFromJson(json);
  Map<String, dynamic> toJson() => _$RejectCostumeTransferRequestToJson(this);
}

@JsonSerializable()
class CostumeReminderRequest {
  const CostumeReminderRequest({this.memberIds, this.message});

  final List<int>? memberIds;
  final String? message;

  factory CostumeReminderRequest.fromJson(Map<String, dynamic> json) => _$CostumeReminderRequestFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeReminderRequestToJson(this);
}

@JsonSerializable()
class CostumeReminderResponse {
  const CostumeReminderResponse({required this.recipients, required this.reached});

  final int recipients;
  final int reached;

  factory CostumeReminderResponse.fromJson(Map<String, dynamic> json) => _$CostumeReminderResponseFromJson(json);
  Map<String, dynamic> toJson() => _$CostumeReminderResponseToJson(this);
}

/// Vytvořené id z API (`{ id }`).
@JsonSerializable()
class CreatedId {
  const CreatedId({required this.id});

  final int id;

  factory CreatedId.fromJson(Map<String, dynamic> json) => _$CreatedIdFromJson(json);
  Map<String, dynamic> toJson() => _$CreatedIdToJson(this);
}
