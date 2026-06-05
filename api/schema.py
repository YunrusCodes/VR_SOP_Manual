"""Pydantic models shared between the structured-JSON endpoints and the CSV
converter. CSV remains the source of truth on disk — these models exist so the
admin frontend can edit a clean structured payload instead of fighting with the
13-column CSV (three exception slots etc.)."""
from __future__ import annotations

from typing import Literal, Optional

from pydantic import BaseModel, Field

MediaKind = Literal["image", "video", "none"]


class Media(BaseModel):
    kind: MediaKind = "none"
    filename: Optional[str] = None


class ExceptionAction(BaseModel):
    type: Literal["goToStep", "showMessage"]
    # exactly one of these is populated, matching `type`
    step: Optional[int] = None
    text: Optional[str] = None


class ExceptionOption(BaseModel):
    label: str
    action: ExceptionAction


class Step(BaseModel):
    order: int
    mainTitle: str = ""
    subTitle: str = ""
    name: str = ""
    description: str = ""
    media: Media = Field(default_factory=Media)
    nextStepIndication: str = ""
    exceptions: list[ExceptionOption] = Field(default_factory=list)


class Course(BaseModel):
    name: str
    displayName: str
    introduction: str = ""
    steps: list[Step] = Field(default_factory=list)


class CourseMeta(BaseModel):
    displayName: str


class CreateCoursePayload(BaseModel):
    name: str
    displayName: str
    introduction: str = ""
